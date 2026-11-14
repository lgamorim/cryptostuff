namespace CryptoStuff.Core.Alerts;

/// <summary>
/// The default <see cref="IAlertEvaluator"/>, loading alerts through an
/// <see cref="IAlertRepository"/>, pricing them through an
/// <see cref="ICryptocurrencyService"/>, and reading the time from a
/// <see cref="TimeProvider"/>.
/// </summary>
public sealed class AlertEvaluator(
    IAlertRepository repository,
    ICryptocurrencyService cryptocurrencyService,
    TimeProvider timeProvider) : IAlertEvaluator
{
    /// <inheritdoc />
    public async Task<ServiceResult<AlertEvaluationView>> EvaluateAsync(CancellationToken cancellationToken)
    {
        var alerts = await repository.ListArmedAsync(cancellationToken);
        if (alerts.Count == 0)
        {
            return Success([], []);
        }

        var priced = await PriceAsync(alerts, cancellationToken);
        if (priced.ErrorCode is { } errorCode)
        {
            return ServiceResult<AlertEvaluationView>.Failure(errorCode, priced.ErrorDetail);
        }

        // `!` is safe: a successful result always carries its value.
        var (pricedAlerts, unpriced) = priced.Value!;
        return Success(await TriggerAsync(pricedAlerts, cancellationToken), unpriced);
    }

    /// <summary>
    /// Prices every alert in its currency with one lookup for the union of
    /// their coins and currencies, all before any alert is triggered, so a
    /// failure leaves every alert armed. An alert left without a price, its
    /// coin unknown to CoinGecko or not quoted in its currency, is reported
    /// unpriced instead of failing the others: it stays armed, and the next
    /// evaluation tries it again.
    /// </summary>
    private async Task<ServiceResult<PricedAlerts>> PriceAsync(IReadOnlyList<PriceAlert> alerts, CancellationToken cancellationToken)
    {
        var priceQuery = new CoinPriceQuery
        {
            CoinIds = [.. alerts.Select(a => a.CoinId).Distinct()],
            VsCurrencies = [.. alerts.Select(a => a.VsCurrency).Distinct()],
        };
        var prices = await cryptocurrencyService.GetKnownCoinPricesAsync(priceQuery, cancellationToken);
        if (prices.ErrorCode is { } errorCode)
        {
            return ServiceResult<PricedAlerts>.Failure(errorCode, prices.ErrorDetail);
        }

        // `!` is safe: a successful result always carries its value.
        var coins = prices.Value!.Coins;
        var priced = new List<PricedAlert>();
        var unpriced = new List<UnpricedAlertView>();
        foreach (var alert in alerts)
        {
            var amount = coins.FirstOrDefault(c => c.CoinId == alert.CoinId)?.Amounts.FirstOrDefault(a => a.Currency == alert.VsCurrency);
            if (amount is null)
            {
                unpriced.Add(new UnpricedAlertView { Id = alert.Id, CoinId = alert.CoinId, VsCurrency = alert.VsCurrency });
            }
            else
            {
                priced.Add(new PricedAlert(alert, amount.Amount));
            }
        }

        return ServiceResult<PricedAlerts>.Success(new PricedAlerts(priced, unpriced));
    }

    /// <summary>
    /// Triggers each alert its price meets and saves it on its own, so an
    /// alert changed concurrently costs only its own trigger. Every trigger
    /// carries the same time, read once after the prices arrived.
    /// </summary>
    private async Task<List<TriggeredAlertView>> TriggerAsync(IReadOnlyList<PricedAlert> priced, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var triggered = new List<TriggeredAlertView>();
        foreach (var (alert, price) in priced)
        {
            // A refused trigger means the alert was not armed after all; it is left as it is.
            if (!alert.IsMetBy(price) || !alert.Trigger(price, now).IsValid)
            {
                continue;
            }

            if (!await TrySaveAsync(alert, cancellationToken))
            {
                continue;
            }

            triggered.Add(new TriggeredAlertView
            {
                Id = alert.Id,
                CoinId = alert.CoinId,
                VsCurrency = alert.VsCurrency,
                Direction = alert.Direction,
                Threshold = alert.Threshold,
                Price = price,
                TriggeredAt = now,
            });
        }

        return triggered;
    }

    /// <summary>
    /// Saves the trigger just made to <paramref name="alert"/>, returning
    /// false, with the alert dropped, when another writer changed or deleted it
    /// since it was read. A failed save leaves its change pending, so without
    /// the discard every later save would fail on the same stale alert; the
    /// next run finds the alert as it now is. Any other exception, including
    /// cancellation, propagates.
    /// </summary>
    private async Task<bool> TrySaveAsync(PriceAlert alert, CancellationToken cancellationToken)
    {
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (PersistenceConflictException)
        {
            repository.Discard(alert);
            return false;
        }
    }

    private static ServiceResult<AlertEvaluationView> Success(
        IReadOnlyList<TriggeredAlertView> triggered, IReadOnlyList<UnpricedAlertView> unpriced) =>
        ServiceResult<AlertEvaluationView>.Success(new AlertEvaluationView { Triggered = triggered, Unpriced = unpriced });

    private sealed record PricedAlert(PriceAlert Alert, decimal Price);

    private sealed record PricedAlerts(IReadOnlyList<PricedAlert> Priced, IReadOnlyList<UnpricedAlertView> Unpriced);
}
