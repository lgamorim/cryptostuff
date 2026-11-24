namespace CryptoStuff.Core;

/// <summary>
/// The name of the one <see cref="System.Diagnostics.Metrics.Meter"/> every
/// upstream-call instrument is created on: the provider client's request
/// counter, the rate-limit retry counter, and the cache's lookup counter.
/// It lives here because Core is the one project all three recorders
/// reference.
/// </summary>
public static class UpstreamMetrics
{
    /// <summary>The meter name: <c>cryptostuff.upstream</c>.</summary>
    public const string MeterName = "cryptostuff.upstream";
}
