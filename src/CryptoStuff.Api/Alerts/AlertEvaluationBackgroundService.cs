using System.Text.Json;
using CryptoStuff.Composition;
using CryptoStuff.Core;
using CryptoStuff.Core.Alerts;

namespace CryptoStuff.Api.Alerts;

/// <summary>
/// Evaluates the armed price alerts on a fixed interval while the API host
/// runs: the first evaluation one interval after start-up, then one per
/// interval, each in a scope of its own so it gets a fresh database context.
/// Each triggered alert is logged. A failed evaluation, whether an error
/// result or an exception, is logged and costs only its own tick, since an
/// exception escaping here would stop the host; shutdown ends the loop,
/// including an evaluation in progress, without logging a failure.
/// </summary>
public sealed partial class AlertEvaluationBackgroundService(
    IServiceScopeFactory scopeFactory,
    AlertEvaluationSchedule schedule,
    TimeProvider timeProvider,
    ILogger<AlertEvaluationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(schedule.Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await EvaluateOnceAsync(stoppingToken);
            }
            // Once shutdown has begun, whatever the tick throws ends the loop:
            // the cancellation shutdown causes is how the host expects a
            // background service to stop, and the host logs anything else.
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                LogEvaluationThrew(exception);
            }
        }
    }

    private async Task EvaluateOnceAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var evaluator = scope.ServiceProvider.GetRequiredService<IAlertEvaluator>();
        var result = await evaluator.EvaluateAsync(stoppingToken);
        if (result.ErrorCode is { } errorCode)
        {
            LogEvaluationFailed(errorCode, result.ErrorDetail);
            return;
        }

        // `!` is safe: a successful result always carries its value.
        foreach (var alert in result.Value!.Triggered)
        {
            LogTriggered(alert.Id, alert.CoinId, alert.Price, alert.VsCurrency, ToToken(alert.Direction), alert.Threshold);
        }
    }

    private static string ToToken(AlertDirection direction) => JsonNamingPolicy.KebabCaseLower.ConvertName(direction.ToString());

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Alert {AlertId} triggered: {CoinId} is at {Price} {VsCurrency}, meeting its condition {Direction} {Threshold}.")]
    private partial void LogTriggered(int alertId, string coinId, decimal price, string vsCurrency, string direction, decimal threshold);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Alert evaluation failed with {ErrorCode}: {ErrorDetail} Every alert stays armed until the next evaluation.")]
    private partial void LogEvaluationFailed(ServiceErrorCode errorCode, string? errorDetail);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error,
        Message = "Alert evaluation threw an exception. Every alert it had not yet triggered stays armed until the next evaluation.")]
    private partial void LogEvaluationThrew(Exception exception);
}
