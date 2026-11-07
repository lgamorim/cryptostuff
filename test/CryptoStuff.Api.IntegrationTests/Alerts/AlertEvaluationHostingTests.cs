using CryptoStuff.Api.Alerts;
using CryptoStuff.Api.IntegrationTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CryptoStuff.Api.IntegrationTests.Alerts;

public class AlertEvaluationHostingTests
{
    [Fact]
    public async Task Should_HostTheAlertEvaluationBackgroundService_When_TheApiRuns()
    {
        await using var factory = new CryptoStuffApiFactory(hostAlertEvaluation: true);

        var hostedServices = factory.Services.GetServices<IHostedService>();

        hostedServices.OfType<AlertEvaluationBackgroundService>().Should().ContainSingle();
    }

    [Fact]
    public async Task Should_NotHostTheAlertEvaluationBackgroundService_When_TheFactoryKeepsTestsDeterministic()
    {
        await using var factory = new CryptoStuffApiFactory();

        var hostedServices = factory.Services.GetServices<IHostedService>();

        hostedServices.OfType<AlertEvaluationBackgroundService>().Should().BeEmpty();
    }
}
