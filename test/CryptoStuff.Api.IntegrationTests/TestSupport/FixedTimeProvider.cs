namespace CryptoStuff.Api.IntegrationTests.TestSupport;

/// <summary>A clock that always reads the instant it was given.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
