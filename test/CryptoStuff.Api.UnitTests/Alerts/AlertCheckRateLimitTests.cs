using CryptoStuff.Api.Alerts;
using Microsoft.Extensions.Configuration;

namespace CryptoStuff.Api.UnitTests.Alerts;

// The integration tests cover the setting end to end, but there the API's own
// appsettings.json always sets it, so only here can it be truly absent.
public class AlertCheckRateLimitTests
{
    [Fact]
    public void Should_AllowSixChecksAMinute_When_ChecksPerMinuteIsAbsent()
    {
        var configuration = new ConfigurationBuilder().Build();

        var checksPerMinute = AlertCheckRateLimit.ReadChecksPerMinute(configuration);

        checksPerMinute.Should().Be(6);
    }

    [Fact]
    public void Should_AllowTheConfiguredChecks_When_ChecksPerMinuteIsSet()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Alerts:ChecksPerMinute"] = "3" })
            .Build();

        var checksPerMinute = AlertCheckRateLimit.ReadChecksPerMinute(configuration);

        checksPerMinute.Should().Be(3);
    }
}
