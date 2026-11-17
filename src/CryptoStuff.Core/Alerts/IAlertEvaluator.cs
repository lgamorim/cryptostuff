namespace CryptoStuff.Core.Alerts;

/// <summary>
/// The single evaluation both hosts run: check every armed alert against
/// current prices and trigger the ones that are met, except a re-armed alert
/// still awaiting its reset, which a price that does not meet it resets
/// instead.
/// </summary>
public interface IAlertEvaluator
{
    /// <summary>
    /// Evaluates the armed alerts once and reports the ones that triggered and
    /// the ones it could not price, which stay armed. Its triggers and resets
    /// are saved together, so an evaluation that fails, returns
    /// <see cref="ServiceErrorCode.Conflict"/> because another writer changed
    /// one of the alerts, or throws (a busy database, cancellation) has
    /// triggered nothing.
    /// </summary>
    Task<ServiceResult<AlertEvaluationView>> EvaluateAsync(CancellationToken cancellationToken);
}
