namespace CryptoStuff.Api.IntegrationTests.TestSupport;

/// <summary>The one-shot write <see cref="InterleavingPortfolioRepository"/> runs before the next save once armed.</summary>
internal sealed class CompetingWrite
{
    private Func<CancellationToken, Task>? _armed;

    public void Arm(Func<CancellationToken, Task> write) => _armed = write;

    /// <summary>Returns the armed write and disarms it, or null when none is armed.</summary>
    public Func<CancellationToken, Task>? TakeArmed() => Interlocked.Exchange(ref _armed, null);
}
