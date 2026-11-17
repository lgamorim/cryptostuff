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
    private const string ChangedConcurrentlyDetail =
        "Another operation changed an alert while the check ran, so nothing was triggered; run the check again.";

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
        var (triggered, changed) = Trigger(pricedAlerts);
        if (changed.Count == 0)
        {
            return Success(triggered, unpriced);
        }

        return await SaveAllAsync(changed, cancellationToken)
            ? Success(triggered, unpriced)
            : ServiceResult<AlertEvaluationView>.Failure(ServiceErrorCode.Conflict, ChangedConcurrentlyDetail);
    }

    /// <summary>
    /// Prices every alert in its currency with one lookup for the union of
    /// their coins and currencies, all before any alert is triggered, so a
    /// failure leaves every alert armed. An alert left without a price, its
    /// coin unknown to the market data provider or not quoted in its currency, is reported
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
        var table = new PriceTable(prices.Value!.Coins);
        var priced = new List<PricedAlert>();
        var unpriced = new List<UnpricedAlertView>();
        foreach (var alert in alerts)
        {
            if (table.TryGetAmount(alert.CoinId, alert.VsCurrency, out var price))
            {
                priced.Add(new PricedAlert(alert, price));
            }
            else
            {
                unpriced.Add(new UnpricedAlertView { Id = alert.Id, CoinId = alert.CoinId, VsCurrency = alert.VsCurrency });
            }
        }

        return ServiceResult<PricedAlerts>.Success(new PricedAlerts(priced, unpriced));
    }

    /// <summary>
    /// Triggers each alert its price meets, and resets each re-armed alert
    /// whose price has moved back, so a later evaluation can trigger it; none
    /// of it is saved yet. Every trigger carries the same time, read once
    /// after the prices arrived.
    /// </summary>
    private (List<TriggeredAlertView> Triggered, List<PriceAlert> Changed) Trigger(IReadOnlyList<PricedAlert> priced)
    {
        var now = timeProvider.GetUtcNow();
        var triggered = new List<TriggeredAlertView>();
        var changed = new List<PriceAlert>();
        foreach (var (alert, price) in priced)
        {
            if (alert.ResetIfNotMetBy(price))
            {
                changed.Add(alert);
                continue;
            }

            // A refused trigger means the alert was not armed after all, or is
            // still awaiting its reset; it is left as it is.
            if (!alert.IsMetBy(price) || !alert.Trigger(price, now).IsValid)
            {
                continue;
            }

            changed.Add(alert);
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

        return (triggered, changed);
    }

    /// <summary>
    /// Saves every trigger and reset of the run in one save, so the run writes
    /// all of them or none: a failure partway, whether a busy database or
    /// cancellation, never leaves an alert triggered that the run did not get
    /// to report. Returns false, with every change dropped, when another
    /// writer changed or deleted one of the alerts since it was read; a failed
    /// save leaves its changes pending, so without the discard every later
    /// save in the same scope would fail again on the same stale alert. The
    /// next run finds the alerts as they now are. Any other exception,
    /// including cancellation, propagates.
    /// </summary>
    private async Task<bool> SaveAllAsync(IReadOnlyList<PriceAlert> changed, CancellationToken cancellationToken)
    {
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (PersistenceConflictException)
        {
            foreach (var alert in changed)
            {
                repository.Discard(alert);
            }

            return false;
        }
    }

    private static ServiceResult<AlertEvaluationView> Success(
        IReadOnlyList<TriggeredAlertView> triggered, IReadOnlyList<UnpricedAlertView> unpriced) =>
        ServiceResult<AlertEvaluationView>.Success(new AlertEvaluationView { Triggered = triggered, Unpriced = unpriced });

    private sealed record PricedAlert(PriceAlert Alert, decimal Price);

    private sealed record PricedAlerts(IReadOnlyList<PricedAlert> Priced, IReadOnlyList<UnpricedAlertView> Unpriced);
}
