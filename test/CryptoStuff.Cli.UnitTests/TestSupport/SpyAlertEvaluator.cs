using CryptoStuff.Core;
using CryptoStuff.Core.Alerts;

namespace CryptoStuff.Cli.UnitTests.TestSupport;

internal sealed class SpyAlertEvaluator : IAlertEvaluator
{
    public int CallCount { get; private set; }

    public CancellationToken CapturedCancellationToken { get; private set; }

    public Exception? ExceptionToThrow { get; set; }

    public ServiceResult<AlertEvaluationView> Result { get; set; } =
        ServiceResult<AlertEvaluationView>.Success(new AlertEvaluationView { Triggered = [], Unpriced = [] });

    public Task<ServiceResult<AlertEvaluationView>> EvaluateAsync(CancellationToken cancellationToken)
    {
        CallCount++;
        CapturedCancellationToken = cancellationToken;

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return Task.FromResult(Result);
    }
}
