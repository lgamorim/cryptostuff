using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CryptoStuff.Api;

/// <summary>
/// The readiness probe's one check: healthy when <paramref name="isReady"/>
/// reports the shared database ready, unhealthy otherwise. Takes the check as a
/// delegate, as <see cref="StartupDatabaseMigration"/> takes the migration, so
/// this host never touches persistence types.
/// </summary>
public sealed class DatabaseReadinessHealthCheck(Func<CancellationToken, Task<bool>> isReady) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await isReady(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
}
