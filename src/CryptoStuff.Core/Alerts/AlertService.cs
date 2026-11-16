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
        // the market data provider is asked anything.
        var alert = PriceAlert.Create(command.CoinId, command.VsCurrency, command.Direction, command.Threshold);
        if (await UpstreamCoinChecks.CheckPricedAsync<AlertView>(cryptocurrencyService, alert.CoinId, alert.VsCurrency, cancellationToken) is { } rejected)
        {
            return rejected;
        }

        repository.Add(alert);
        return await SaveAsync(alert, cancellationToken) == SaveOutcome.Saved
            ? Success(alert)
            // Alerts share no unique column, so the only conflict an insert
            // can meet is another alert stored with the same id at the same time.
            : ServiceResult<AlertView>.Failure(ServiceErrorCode.Conflict, "The alert could not be saved because it clashed with an alert stored at the same time; try again.");
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
        return await SaveAsync(alert, cancellationToken) switch
        {
            SaveOutcome.Saved => ServiceResult.Success(),
            SaveOutcome.Deleted => ServiceResult.Failure(ServiceErrorCode.NotFound, NotFoundDetail(id)),
            _ => ServiceResult.Failure(ServiceErrorCode.Conflict, ChangedConcurrentlyDetail(id)),
        };
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

        // An alert that is already armed has nothing to save, so a retried
        // re-arm returns the alert the first one left.
        return alert.Rearm()
            ? await SaveChangeAsync(alert, cancellationToken)
            : Success(alert);
    }

    /// <summary>
    /// Saves the change just made to <paramref name="alert"/>, or reports a
    /// lost race as a <see cref="ServiceErrorCode.Conflict"/>, or as
    /// <see cref="ServiceErrorCode.NotFound"/> when another writer deleted it.
    /// </summary>
    private async Task<ServiceResult<AlertView>> SaveChangeAsync(PriceAlert alert, CancellationToken cancellationToken) =>
        await SaveAsync(alert, cancellationToken) switch
        {
            SaveOutcome.Saved => Success(alert),
            SaveOutcome.Deleted => NotFound(alert.Id),
            _ => ServiceResult<AlertView>.Failure(ServiceErrorCode.Conflict, ChangedConcurrentlyDetail(alert.Id)),
        };

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

    /// <summary>Saves the pending change to <paramref name="alert"/>, as <see cref="RecordSave.SaveAsync"/> describes.</summary>
    private Task<SaveOutcome> SaveAsync(PriceAlert alert, CancellationToken cancellationToken) =>
        RecordSave.SaveAsync(repository.SaveChangesAsync, () => repository.Discard(alert), () => alert.Id, repository.ExistsAsync, cancellationToken);

    private static ServiceResult<AlertView> Success(PriceAlert alert) =>
        ServiceResult<AlertView>.Success(AlertMapper.ToView(alert));

    private static ServiceResult<AlertView> NotFound(int id) =>
        ServiceResult<AlertView>.Failure(ServiceErrorCode.NotFound, NotFoundDetail(id));

    private static string NotFoundDetail(int id) => $"No alert has id {id}.";
}
