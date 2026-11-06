namespace CryptoStuff.Core.Alerts;

/// <summary>
/// The default <see cref="IAlertService"/>, storing alerts through an
/// <see cref="IAlertRepository"/> and checking coins through an
/// <see cref="ICryptocurrencyService"/>.
/// </summary>
public sealed class AlertService(
    IAlertRepository repository,
    ICryptocurrencyService cryptocurrencyService) : IAlertService
{
    /// <inheritdoc />
    public async Task<ServiceResult<IReadOnlyList<AlertView>>> ListAsync(CancellationToken cancellationToken)
    {
        var alerts = await repository.ListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<AlertView>>.Success(alerts.Select(AlertMapper.ToView).ToArray());
    }

    /// <inheritdoc />
    public async Task<ServiceResult<AlertView>> GetAsync(int id, CancellationToken cancellationToken)
    {
        var alert = await repository.FindAsync(id, cancellationToken);
        return alert is null ? NotFound(id) : Success(alert);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<AlertView>> CreateAsync(CreateAlertCommand command, CancellationToken cancellationToken)
    {
        // Built first, so details that break the entity's rules throw before
        // CoinGecko is asked anything.
        var alert = PriceAlert.Create(command.CoinId, command.VsCurrency, command.Direction, command.Threshold);
        if (await CheckCoinUpstreamAsync(alert, cancellationToken) is { } rejected)
        {
            return rejected;
        }

        repository.Add(alert);
        return await TrySaveChangesAsync(cancellationToken)
            ? Success(alert)
            : ServiceResult<AlertView>.Failure(ServiceErrorCode.Conflict, "The alert could not be saved because another operation changed the stored alerts; try again.");
    }

    /// <inheritdoc />
    public async Task<ServiceResult<AlertView>> UpdateAsync(UpdateAlertCommand command, CancellationToken cancellationToken)
    {
        var alert = await repository.FindAsync(command.AlertId, cancellationToken);
        if (alert is null)
        {
            return NotFound(command.AlertId);
        }

        if (StaleVersionDetail(alert, command.ExpectedVersion) is { } staleVersion)
        {
            return ServiceResult<AlertView>.Failure(ServiceErrorCode.PreconditionFailed, staleVersion);
        }

        alert.Update(command.Direction, command.Threshold);
        return await SaveChangeAsync(alert, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(int id, int? expectedVersion, CancellationToken cancellationToken)
    {
        var alert = await repository.FindAsync(id, cancellationToken);
        if (alert is null)
        {
            return ServiceResult.Failure(ServiceErrorCode.NotFound, NotFoundDetail(id));
        }

        if (StaleVersionDetail(alert, expectedVersion) is { } staleVersion)
        {
            return ServiceResult.Failure(ServiceErrorCode.PreconditionFailed, staleVersion);
        }

        repository.Remove(alert);
        return await TrySaveChangesAsync(cancellationToken)
            ? ServiceResult.Success()
            : ServiceResult.Failure(ServiceErrorCode.Conflict, ChangedConcurrentlyDetail(id));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<AlertView>> RearmAsync(int id, int? expectedVersion, CancellationToken cancellationToken)
    {
        var alert = await repository.FindAsync(id, cancellationToken);
        if (alert is null)
        {
            return NotFound(id);
        }

        if (StaleVersionDetail(alert, expectedVersion) is { } staleVersion)
        {
            return ServiceResult<AlertView>.Failure(ServiceErrorCode.PreconditionFailed, staleVersion);
        }

        var outcome = alert.Rearm();
        return outcome.IsValid
            ? await SaveChangeAsync(alert, cancellationToken)
            : ServiceResult<AlertView>.Failure(ServiceErrorCode.Conflict, outcome.ErrorMessage);
    }

    /// <summary>
    /// Checks with CoinGecko that the coin exists and is priced in the
    /// alert's vs_currency — the price lookup evaluation will make — returning
    /// the failure to report, or null when both hold. Either one missing is a
    /// <see cref="ServiceErrorCode.RuleViolated"/>; any other upstream failure
    /// is passed on unchanged.
    /// </summary>
    private async Task<ServiceResult<AlertView>?> CheckCoinUpstreamAsync(PriceAlert alert, CancellationToken cancellationToken)
    {
        var priceQuery = new CoinPriceQuery { CoinIds = [alert.CoinId], VsCurrencies = [alert.VsCurrency] };
        var prices = await cryptocurrencyService.GetPricesAsync(priceQuery, cancellationToken);
        if (prices.ErrorCode is { } errorCode)
        {
            return errorCode == ServiceErrorCode.NotFound
                ? ServiceResult<AlertView>.Failure(ServiceErrorCode.RuleViolated, $"CoinGecko has no coin with id '{alert.CoinId}'.")
                : ServiceResult<AlertView>.Failure(errorCode, prices.ErrorDetail);
        }

        // A currency CoinGecko doesn't know is not an error upstream: the
        // coin just comes back without an amount in it.
        // `!` is safe: a successful result always carries its value.
        var pricedInCurrency = prices.Value!.Coins.Any(c => c.Amounts.Any(a => a.Currency == alert.VsCurrency));
        return pricedInCurrency
            ? null
            : ServiceResult<AlertView>.Failure(ServiceErrorCode.RuleViolated, $"CoinGecko has no price for '{alert.CoinId}' in '{alert.VsCurrency}'.");
    }

    /// <summary>
    /// Saves the change just made to <paramref name="alert"/>, or reports a
    /// lost race as a <see cref="ServiceErrorCode.Conflict"/>.
    /// </summary>
    private async Task<ServiceResult<AlertView>> SaveChangeAsync(PriceAlert alert, CancellationToken cancellationToken) =>
        await TrySaveChangesAsync(cancellationToken)
            ? Success(alert)
            : ServiceResult<AlertView>.Failure(ServiceErrorCode.Conflict, ChangedConcurrentlyDetail(alert.Id));

    /// <summary>
    /// Returns the detail to report when the caller expected
    /// <paramref name="alert"/> at a version other than its own, or null
    /// when it expected none or the one it is at.
    /// </summary>
    private static string? StaleVersionDetail(PriceAlert alert, int? expectedVersion) =>
        expectedVersion is { } expected && expected != alert.Version
            ? $"Alert {alert.Id} is at version {alert.Version}, not the expected version {expected}; reload it and try again."
            : null;

    private static string ChangedConcurrentlyDetail(int id) =>
        $"Alert {id} was changed by another operation; reload it and try again.";

    /// <summary>
    /// Saves pending changes, returning false when the repository reports a
    /// <see cref="PersistenceConflictException"/>. Any other exception,
    /// including cancellation, propagates.
    /// </summary>
    private async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (PersistenceConflictException)
        {
            return false;
        }
    }

    private static ServiceResult<AlertView> Success(PriceAlert alert) =>
        ServiceResult<AlertView>.Success(AlertMapper.ToView(alert));

    private static ServiceResult<AlertView> NotFound(int id) =>
        ServiceResult<AlertView>.Failure(ServiceErrorCode.NotFound, NotFoundDetail(id));

    private static string NotFoundDetail(int id) => $"No alert has id {id}.";
}
