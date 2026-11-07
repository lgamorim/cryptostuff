using Microsoft.Extensions.Time.Testing;

namespace CryptoStuff.Api.UnitTests.TestSupport;

/// <summary>
/// A <see cref="FakeTimeProvider"/> that signals when its first timer is
/// created, so a test can wait for a background loop to arm its timer before
/// advancing the clock: an advance made earlier would pass the timer by.
/// </summary>
internal sealed class TimerSignallingTimeProvider : FakeTimeProvider
{
    private readonly TaskCompletionSource _timerCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task TimerCreated => _timerCreated.Task;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);
        _timerCreated.TrySetResult();
        return timer;
    }
}
