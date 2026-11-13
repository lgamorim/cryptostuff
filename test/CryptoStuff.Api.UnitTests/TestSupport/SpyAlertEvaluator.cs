using CryptoStuff.Core;
using CryptoStuff.Core.Alerts;

namespace CryptoStuff.Api.UnitTests.TestSupport;

/// <summary>
/// An <see cref="IAlertEvaluator"/> that records each call and returns
/// <see cref="Result"/>, or, for the next calls, the outcomes queued with the
/// <c>Enqueue</c> methods. <see cref="CallStarted"/> lets a test await a call
/// without waiting on real time: each call completes its signal with the
/// time <paramref name="clock"/> showed when it began.
/// </summary>
internal sealed class SpyAlertEvaluator(TimeProvider? clock = null) : IAlertEvaluator
{
    private readonly Lock _lock = new();
    private readonly List<TaskCompletionSource<DateTimeOffset>> _callSignals = [];
    private readonly Queue<Func<CancellationToken, Task<ServiceResult<AlertEvaluationView>>>> _outcomes = new();
    private int _callCount;

    public int CallCount
    {
        get
        {
            lock (_lock)
            {
                return _callCount;
            }
        }
    }

    public CancellationToken CapturedCancellationToken { get; private set; }

    public ServiceResult<AlertEvaluationView> Result { get; set; } =
        ServiceResult<AlertEvaluationView>.Success(new AlertEvaluationView { Triggered = [], Unpriced = [] });

    public void EnqueueResult(ServiceResult<AlertEvaluationView> result) =>
        Enqueue(_ => Task.FromResult(result));

    public void EnqueueException(Exception exception) =>
        Enqueue(_ => Task.FromException<ServiceResult<AlertEvaluationView>>(exception));

    /// <summary>Queues a call that runs until its token is cancelled, then ends as cancelled, as a real evaluation would.</summary>
    public void EnqueueRunUntilCancelled() =>
        Enqueue(async cancellationToken =>
        {
            var cancelled = new TaskCompletionSource<ServiceResult<AlertEvaluationView>>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var registration = cancellationToken.Register(() => cancelled.TrySetCanceled(cancellationToken));
            return await cancelled.Task;
        });

    /// <summary>Completes, with the clock's time, once the call with this zero-based index has begun.</summary>
    public Task<DateTimeOffset> CallStarted(int index)
    {
        lock (_lock)
        {
            return SignalFor(index).Task;
        }
    }

    public Task<ServiceResult<AlertEvaluationView>> EvaluateAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource<DateTimeOffset> signal;
        Func<CancellationToken, Task<ServiceResult<AlertEvaluationView>>>? outcome;
        lock (_lock)
        {
            CapturedCancellationToken = cancellationToken;
            signal = SignalFor(_callCount++);
            _outcomes.TryDequeue(out outcome);
        }

        signal.TrySetResult(clock?.GetUtcNow() ?? default);
        return outcome?.Invoke(cancellationToken) ?? Task.FromResult(Result);
    }

    private void Enqueue(Func<CancellationToken, Task<ServiceResult<AlertEvaluationView>>> outcome)
    {
        lock (_lock)
        {
            _outcomes.Enqueue(outcome);
        }
    }

    // Called under _lock.
    private TaskCompletionSource<DateTimeOffset> SignalFor(int index)
    {
        while (_callSignals.Count <= index)
        {
            _callSignals.Add(new TaskCompletionSource<DateTimeOffset>(TaskCreationOptions.RunContinuationsAsynchronously));
        }

        return _callSignals[index];
    }
}
