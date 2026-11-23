using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CryptoStuff.Api.UnitTests;

public class DatabaseReadinessHealthCheckTests
{
    [Fact]
    public async Task Should_ReportHealthy_When_DatabaseIsReady()
    {
        var check = new DatabaseReadinessHealthCheck(_ => Task.FromResult(true));

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Should_ReportUnhealthy_When_DatabaseIsNotReady()
    {
        var check = new DatabaseReadinessHealthCheck(_ => Task.FromResult(false));

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task Should_CheckOnceWithTheCallersToken_When_HealthChecked()
    {
        var checks = new List<CancellationToken>();
        var check = new DatabaseReadinessHealthCheck(cancellationToken =>
        {
            checks.Add(cancellationToken);
            return Task.FromResult(true);
        });
        using var caller = new CancellationTokenSource();

        await check.CheckHealthAsync(new HealthCheckContext(), caller.Token);

        checks.Should().Equal(caller.Token);
    }
}
