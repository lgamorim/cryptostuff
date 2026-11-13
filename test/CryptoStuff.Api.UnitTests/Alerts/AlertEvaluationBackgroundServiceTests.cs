using System.Collections.Concurrent;
using CryptoStuff.Api.Alerts;
using CryptoStuff.Api.UnitTests.TestSupport;
using CryptoStuff.Composition;
using CryptoStuff.Core;
using CryptoStuff.Core.Alerts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CryptoStuff.Api.UnitTests.Alerts;

/// <summary>
/// Drives <see cref="AlertEvaluationBackgroundService"/> through a fake clock:
/// each test waits for the loop to arm its timer, advances the clock, and
/// awaits the evaluator's per-call signal, so nothing depends on real time.
/// </summary>
public sealed class AlertEvaluationBackgroundServiceTests : IAsyncDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    // A real-time bound on every await that waits for the loop, so a broken
    // loop fails the test instead of hanging the run. A passing test never
    // gets near it: each awaited step completes as soon as the fake clock moves.
    private static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(5);

    private readonly TimerSignallingTimeProvider _time = new();
    private readonly SpyAlertEvaluator _evaluator;
    private readonly ConcurrentQueue<IServiceProvider> _evaluatorResolvedFrom = new();
    private readonly ConcurrentQueue<ScopeProbe> _scopeProbes = new();
    private readonly SpyLogger<AlertEvaluationBackgroundService> _logger = new();
    private readonly ServiceProvider _provider;
    private readonly AlertEvaluationBackgroundService _service;

    public AlertEvaluationBackgroundServiceTests()
    {
        _evaluator = new SpyAlertEvaluator(_time);

        var services = new ServiceCollection();
        services.AddScoped<ScopeProbe>();
        services.AddScoped<IAlertEvaluator>(scopedProvider =>
        {
            _evaluatorResolvedFrom.Enqueue(scopedProvider);
            _scopeProbes.Enqueue(scopedProvider.GetRequiredService<ScopeProbe>());
            return _evaluator;
        });
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        _service = new AlertEvaluationBackgroundService(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            new AlertEvaluationSchedule { Interval = Interval },
            _time,
            _logger);
    }

    [Fact]
    public async Task Should_NotEvaluate_When_TheFirstIntervalHasNotElapsed()
    {
        var startedAt = _time.GetUtcNow();
        await StartAsync();

        _time.Advance(Interval - TimeSpan.FromTicks(1));
        _time.Advance(TimeSpan.FromTicks(1));
        var firstEvaluatedAt = await CallStartedAsync(0);

        firstEvaluatedAt.Should().Be(startedAt + Interval);
    }

    [Fact]
    public async Task Should_EvaluateOncePerInterval_When_TheClockAdvancesIntervalByInterval()
    {
        var startedAt = _time.GetUtcNow();
        await StartAsync();

        var evaluatedAt = new List<DateTimeOffset>();
        for (var tick = 0; tick < 3; tick++)
        {
            _time.Advance(Interval);
            evaluatedAt.Add(await CallStartedAsync(tick));
        }

        evaluatedAt.Should().Equal(startedAt + Interval, startedAt + (2 * Interval), startedAt + (3 * Interval));
    }

    [Fact]
    public async Task Should_ResolveTheEvaluatorFromAFreshScope_When_EachTickRuns()
    {
        await StartAsync();

        _time.Advance(Interval);
        await CallStartedAsync(0);
        _time.Advance(Interval);
        await CallStartedAsync(1);

        _evaluatorResolvedFrom.Should().HaveCount(2).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Should_DisposeTheTickScope_When_TheTickEnds()
    {
        await StartAsync();

        _time.Advance(Interval);
        await CallStartedAsync(0);

        // The loop is sequential: the second tick begins only after the first ended.
        _time.Advance(Interval);
        await CallStartedAsync(1);

        _scopeProbes.First().IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task Should_PassTheStoppingTokenToTheEvaluator_When_Evaluating()
    {
        await StartAsync();
        _time.Advance(Interval);
        await CallStartedAsync(0);

        await StopServiceAsync();

        _evaluator.CapturedCancellationToken.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task Should_LogEachTriggeredAlert_When_AnEvaluationTriggersAlerts()
    {
        _evaluator.EnqueueResult(ServiceResult<AlertEvaluationView>.Success(new AlertEvaluationView
        {
            Triggered =
            [
                Triggered(3, "bitcoin", "usd", AlertDirection.Above, 50000m, 52000.5m),
                Triggered(8, "ethereum", "eur", AlertDirection.Below, 2000m, 1999m),
            ],
            Unpriced = [],
        }));
        await StartAsync();
        _time.Advance(Interval);
        await CallStartedAsync(0);

        // Stopping waits for the tick in progress to end.
        await StopServiceAsync();

        _logger.Entries.Should().Equal(
            [
                new LogEntry(LogLevel.Information, "Alert 3 triggered: bitcoin is at 52000.5 usd, meeting its condition above 50000.", null),
                new LogEntry(LogLevel.Information, "Alert 8 triggered: ethereum is at 1999 eur, meeting its condition below 2000.", null),
            ]);
    }

    [Fact]
    public async Task Should_WarnOnceAboutUnpricedAlerts_When_ConsecutiveEvaluationsLeaveTheSameAlertsUnpriced()
    {
        _evaluator.EnqueueResult(Evaluation(Unpriced(5, "ghostcoin", "usd"), Unpriced(9, "bitcoin", "eur")));
        _evaluator.EnqueueResult(Evaluation(Unpriced(5, "ghostcoin", "usd"), Unpriced(9, "bitcoin", "eur")));
        await StartAsync();

        _time.Advance(Interval);
        await CallStartedAsync(0);
        _time.Advance(Interval);
        await CallStartedAsync(1);
        await StopServiceAsync();

        _logger.Entries.Should().Equal([UnpricedWarning("5 (ghostcoin in usd), 9 (bitcoin in eur)")]);
    }

    [Fact]
    public async Task Should_WarnAgainNamingTheNewAlerts_When_TheUnpricedAlertsChange()
    {
        _evaluator.EnqueueResult(Evaluation(Unpriced(5, "ghostcoin", "usd")));
        _evaluator.EnqueueResult(Evaluation(Unpriced(5, "ghostcoin", "usd"), Unpriced(9, "bitcoin", "eur")));
        await StartAsync();

        _time.Advance(Interval);
        await CallStartedAsync(0);
        _time.Advance(Interval);
        await CallStartedAsync(1);
        await StopServiceAsync();

        _logger.Entries.Should().Equal(
            [
                UnpricedWarning("5 (ghostcoin in usd)"),
                UnpricedWarning("5 (ghostcoin in usd), 9 (bitcoin in eur)"),
            ]);
    }

    [Fact]
    public async Task Should_LogThatEveryAlertWasPriced_When_NoAlertIsUnpricedAnyMore()
    {
        _evaluator.EnqueueResult(Evaluation(Unpriced(5, "ghostcoin", "usd")));
        _evaluator.EnqueueResult(Evaluation());
        await StartAsync();

        _time.Advance(Interval);
        await CallStartedAsync(0);
        _time.Advance(Interval);
        await CallStartedAsync(1);
        await StopServiceAsync();

        _logger.Entries.Should().Equal(
            [
                UnpricedWarning("5 (ghostcoin in usd)"),
                new LogEntry(LogLevel.Information, "Every armed alert was priced again.", null),
            ]);
    }

    [Fact]
    public async Task Should_NotWarnAgainAboutUnpricedAlerts_When_AFailedEvaluationComesBetweenTheSameAlertsUnpriced()
    {
        _evaluator.EnqueueResult(Evaluation(Unpriced(5, "ghostcoin", "usd")));
        _evaluator.EnqueueResult(ServiceResult<AlertEvaluationView>.Failure(ServiceErrorCode.RateLimited, "Try again in a minute."));
        _evaluator.EnqueueResult(Evaluation(Unpriced(5, "ghostcoin", "usd")));
        await StartAsync();

        for (var tick = 0; tick < 3; tick++)
        {
            _time.Advance(Interval);
            await CallStartedAsync(tick);
        }

        await StopServiceAsync();

        _logger.Entries.Should().Equal(
            [
                UnpricedWarning("5 (ghostcoin in usd)"),
                new LogEntry(
                    LogLevel.Warning,
                    "Alert evaluation failed with RateLimited: Try again in a minute. Every alert stays armed until the next evaluation.",
                    null),
            ]);
    }

    [Fact]
    public async Task Should_NotWarnAgainAboutUnpricedAlerts_When_AThrowingEvaluationComesBetweenTheSameAlertsUnpriced()
    {
        var failure = new InvalidOperationException("The database is locked.");
        _evaluator.EnqueueResult(Evaluation(Unpriced(5, "ghostcoin", "usd")));
        _evaluator.EnqueueException(failure);
        _evaluator.EnqueueResult(Evaluation(Unpriced(5, "ghostcoin", "usd")));
        await StartAsync();

        for (var tick = 0; tick < 3; tick++)
        {
            _time.Advance(Interval);
            await CallStartedAsync(tick);
        }

        await StopServiceAsync();

        _logger.Entries.Should().Equal(
            [
                UnpricedWarning("5 (ghostcoin in usd)"),
                new LogEntry(
                    LogLevel.Error,
                    "Alert evaluation threw an exception. Every alert it had not yet triggered stays armed until the next evaluation.",
                    failure),
            ]);
    }

    [Fact]
    public async Task Should_LogAWarningAndEvaluateAgainNextTick_When_AnEvaluationReturnsAnError()
    {
        _evaluator.EnqueueResult(ServiceResult<AlertEvaluationView>.Failure(ServiceErrorCode.RateLimited, "Try again in a minute."));
        await StartAsync();

        _time.Advance(Interval);
        await CallStartedAsync(0);
        _time.Advance(Interval);
        await CallStartedAsync(1);

        _logger.Entries.Should().Equal(
            [
                new LogEntry(
                    LogLevel.Warning,
                    "Alert evaluation failed with RateLimited: Try again in a minute. Every alert stays armed until the next evaluation.",
                    null),
            ]);
    }

    [Fact]
    public async Task Should_LogAnErrorAndEvaluateAgainNextTick_When_AnEvaluationThrows()
    {
        var failure = new InvalidOperationException("The database is locked.");
        _evaluator.EnqueueException(failure);
        await StartAsync();

        _time.Advance(Interval);
        await CallStartedAsync(0);
        _time.Advance(Interval);
        await CallStartedAsync(1);

        _logger.Entries.Should().Equal(
            [
                new LogEntry(
                    LogLevel.Error,
                    "Alert evaluation threw an exception. Every alert it had not yet triggered stays armed until the next evaluation.",
                    failure),
            ]);
    }

    [Fact]
    public async Task Should_EndTheLoopWithoutLoggingAFailure_When_ShutdownCancelsAnEvaluationInProgress()
    {
        _evaluator.EnqueueRunUntilCancelled();
        await StartAsync();
        _time.Advance(Interval);
        await CallStartedAsync(0);

        await StopServiceAsync();
        _time.Advance(Interval);

        // ExecuteTask is set by StartAsync, which this test called first.
        _service.ExecuteTask!.IsCompleted.Should().BeTrue();
        _service.ExecuteTask.IsFaulted.Should().BeFalse();
        _logger.Entries.Should().BeEmpty();
        _evaluator.CallCount.Should().Be(1);
    }

    public async ValueTask DisposeAsync()
    {
        await StopServiceAsync();
        _service.Dispose();
        await _provider.DisposeAsync();
    }

    private static async Task WithinHangGuardAsync(Task task, string description)
    {
        try
        {
            await task.WaitAsync(HangGuard);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"{description} did not happen within {HangGuard.TotalSeconds:0} seconds.");
        }
    }

    private async Task StartAsync()
    {
        await WithinHangGuardAsync(_service.StartAsync(CancellationToken.None), "Starting the service");
        await WithinHangGuardAsync(_time.TimerCreated, "Creating the loop's timer");
    }

    // StopAsync(CancellationToken.None) waits for the loop to end, however long that takes.
    private Task StopServiceAsync() => WithinHangGuardAsync(_service.StopAsync(CancellationToken.None), "Stopping the service");

    /// <summary>
    /// Awaits the evaluator call with this zero-based index, failing at once,
    /// rather than waiting forever, when the loop ends before making it, and
    /// within <see cref="HangGuard"/> when the loop never makes it.
    /// </summary>
    private async Task<DateTimeOffset> CallStartedAsync(int index)
    {
        var call = _evaluator.CallStarted(index);

        // ExecuteTask is set by StartAsync, which every test calls first.
        var executeTask = _service.ExecuteTask!;
        await WithinHangGuardAsync(Task.WhenAny(call, executeTask), $"Evaluation {index + 1}");
        if (!call.IsCompleted)
        {
            // Rethrows whatever ended the loop; a loop that ended quietly fails below.
            await executeTask;
            Assert.Fail($"The evaluation loop ended before evaluation {index + 1}.");
        }

        return await call;
    }

    private static ServiceResult<AlertEvaluationView> Evaluation(params UnpricedAlertView[] unpriced) =>
        ServiceResult<AlertEvaluationView>.Success(new AlertEvaluationView { Triggered = [], Unpriced = unpriced });

    private static UnpricedAlertView Unpriced(int id, string coinId, string vsCurrency) =>
        new() { Id = id, CoinId = coinId, VsCurrency = vsCurrency };

    private static LogEntry UnpricedWarning(string alerts) =>
        new(
            LogLevel.Warning,
            $"Alerts {alerts} could not be priced, so they stay armed. CoinGecko gave no price for them; this is logged again only when the alerts it cannot price change.",
            null);

    private TriggeredAlertView Triggered(int id, string coinId, string vsCurrency, AlertDirection direction, decimal threshold, decimal price) =>
        new()
        {
            Id = id,
            CoinId = coinId,
            VsCurrency = vsCurrency,
            Direction = direction,
            Threshold = threshold,
            Price = price,
            TriggeredAt = _time.GetUtcNow(),
        };

    /// <summary>A scoped service that records its scope's disposal.</summary>
    private sealed class ScopeProbe : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
