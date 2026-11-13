namespace CryptoStuff.Core.Alerts;

/// <summary>
/// The single evaluation both hosts run: check every armed alert against
/// current prices and trigger the ones that are met.
/// </summary>
public interface IAlertEvaluator
{
    /// <summary>
    /// Evaluates the armed alerts once and reports the ones that triggered and
    /// the ones it could not price, which stay armed.
    /// </summary>
    Task<ServiceResult<AlertEvaluationView>> EvaluateAsync(CancellationToken cancellationToken);
}
