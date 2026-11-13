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
    /// Prices every alert in its currency, all before any alert is triggered,
    /// so a failure leaves every alert armed. An alert left without a price,
    /// its coin unknown to CoinGecko or not quoted in its currency, is reported
    /// unpriced instead of failing the others: it stays armed, and the next
    /// evaluation tries it again.
    /// </summary>
    private async Task<ServiceResult<PricedAlerts>> PriceAsync(IReadOnlyList<PriceAlert> alerts, CancellationToken cancellationToken)
    {
        var fetched = await FetchPricesAsync(alerts, cancellationToken);
        if (fetched.ErrorCode is { } errorCode)
        {
            return ServiceResult<PricedAlerts>.Failure(errorCode, fetched.ErrorDetail);
        }

        // `!` is safe: a successful result always carries its value.
        var coins = fetched.Value!;
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
    /// Fetches the prices the alerts need with one upstream call for the union
    /// of their coins and currencies. CoinGecko omits a coin it no longer
    /// knows, which the adapter reports as <see cref="ServiceErrorCode.NotFound"/>
    /// for the whole call without naming the coin; only then is each coin asked
    /// for on its own, in its alerts' currencies, so the coins CoinGecko still
    /// knows are priced and the unknown ones are simply absent. With a single
    /// coin the one call already was that coin's own, so it is not repeated.
    /// The adapter also reports an HTTP <c>404</c> from the price endpoint
    /// itself as <see cref="ServiceErrorCode.NotFound"/>, which cannot be told
    /// apart, so that leaves every coin absent too. Any other failure, from
    /// either kind of call, fails the fetch.
    /// </summary>
    private async Task<ServiceResult<IReadOnlyList<CoinPrice>>> FetchPricesAsync(IReadOnlyList<PriceAlert> alerts, CancellationToken cancellationToken)
    {
        var coins = alerts.GroupBy(a => a.CoinId).ToList();
        var all = await GetPricesAsync(
            [.. coins.Select(c => c.Key)], [.. alerts.Select(a => a.VsCurrency).Distinct()], cancellationToken);
        if (all.ErrorCode != ServiceErrorCode.NotFound)
        {
            return all;
        }

        var found = new List<CoinPrice>();
        if (coins.Count == 1)
        {
            return ServiceResult<IReadOnlyList<CoinPrice>>.Success(found);
        }

        foreach (var coinAlerts in coins)
        {
            var one = await GetPricesAsync([coinAlerts.Key], [.. coinAlerts.Select(a => a.VsCurrency).Distinct()], cancellationToken);
            if (one.ErrorCode == ServiceErrorCode.NotFound)
            {
                continue;
            }

            if (one.ErrorCode is { } errorCode)
            {
                return ServiceResult<IReadOnlyList<CoinPrice>>.Failure(errorCode, one.ErrorDetail);
            }

            // `!` is safe: a successful result always carries its value.
            found.AddRange(one.Value!);
        }

        return ServiceResult<IReadOnlyList<CoinPrice>>.Success(found);
    }

    private async Task<ServiceResult<IReadOnlyList<CoinPrice>>> GetPricesAsync(
        IReadOnlyList<string> coinIds, IReadOnlyList<string> vsCurrencies, CancellationToken cancellationToken)
    {
        var prices = await cryptocurrencyService.GetPricesAsync(
            new CoinPriceQuery { CoinIds = coinIds, VsCurrencies = vsCurrencies }, cancellationToken);

        // `!` is safe: a successful result always carries its value.
        return prices.ErrorCode is { } errorCode
            ? ServiceResult<IReadOnlyList<CoinPrice>>.Failure(errorCode, prices.ErrorDetail)
            : ServiceResult<IReadOnlyList<CoinPrice>>.Success(prices.Value!.Coins);
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
