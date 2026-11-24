using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using CryptoStuff.Composition.UnitTests.TestSupport;
using CryptoStuff.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoStuff.Composition.UnitTests;

/// <summary>
/// Checks that <see cref="CryptoStuffServiceCollectionExtensions.AddCryptoStuff"/>
/// hands the container's <see cref="IMeterFactory"/> to every recorder. Each
/// test registers its own <see cref="RecordingMeterFactory"/> before
/// <c>AddCryptoStuff</c> (whose <c>AddMetrics</c> only adds one when none is
/// registered), and swaps the CoinGecko client's primary handler for a
/// scripted one, so no request leaves the process. That same seam also checks
/// that a send getting no response ends, through the composed pipeline, as
/// <see cref="ServiceErrorCode.UpstreamUnavailable"/>.
/// </summary>
public sealed class UpstreamMetricsRegistrationTests : IDisposable
{
    private readonly RecordingMeterFactory _meterFactory = new();

    public void Dispose() => _meterFactory.Dispose();

    private ServiceProvider BuildProvider(SequencedHttpMessageHandler primaryHandler)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMeterFactory>(_meterFactory);
        services.AddCryptoStuff(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["CoinGecko:ApiKey"] = "test-api-key" })
            .Build());
        services.AddHttpClient(CryptoStuffServiceCollectionExtensions.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandler);
        return services.BuildServiceProvider();
    }

    private static HttpResponseMessage NotFound() => new(HttpStatusCode.NotFound);

    private IEnumerable<RecordedMeasurement> MeasurementsOf(string instrumentName) =>
        _meterFactory.Measurements.Where(measurement => measurement.InstrumentName == instrumentName);

    [Fact]
    public async Task Should_RecordCacheLookupOnRegisteredMeterFactory_When_CoinRequestedThroughRegisteredService()
    {
        using var provider = BuildProvider(SequencedHttpMessageHandler.ReturningInOrder(NotFound));
        var service = provider.GetRequiredService<ICryptocurrencyService>();

        await service.GetCoinAsync(new CoinQuery { CoinId = "bitcoin" }, CancellationToken.None);

        MeasurementsOf("cryptostuff.cache.lookups").Should().ContainSingle()
            .Which.Tags.Should().Contain("result", "miss");
    }

    [Fact]
    public async Task Should_RecordRequestOnRegisteredMeterFactory_When_CoinRequestedThroughRegisteredService()
    {
        using var provider = BuildProvider(SequencedHttpMessageHandler.ReturningInOrder(NotFound));
        var service = provider.GetRequiredService<ICryptocurrencyService>();

        await service.GetCoinAsync(new CoinQuery { CoinId = "bitcoin" }, CancellationToken.None);

        MeasurementsOf("cryptostuff.upstream.requests").Should().ContainSingle()
            .Which.Tags.Should().Contain("outcome", "not_found");
    }

    [Fact]
    public async Task Should_RecordRetryOnRegisteredMeterFactory_When_RegisteredClientIsRateLimitedOnce()
    {
        // Retry-After: 0 makes the handler's real-clock delay zero-length.
        using var provider = BuildProvider(SequencedHttpMessageHandler.ReturningInOrder(
            () => new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero) } },
            NotFound));
        var service = provider.GetRequiredService<ICryptocurrencyService>();

        await service.GetCoinAsync(new CoinQuery { CoinId = "bitcoin" }, CancellationToken.None);

        MeasurementsOf("cryptostuff.upstream.retries").Should().ContainSingle()
            .Which.Tags.Should().Contain("provider", "coingecko");
        MeasurementsOf("cryptostuff.upstream.requests").Should().ContainSingle();
    }

    [Fact]
    public async Task Should_ReturnUpstreamUnavailableAndRecordNoResponse_When_RegisteredClientGetsNoResponse()
    {
        using var provider = BuildProvider(SequencedHttpMessageHandler.ReturningInOrder(
            () => throw new HttpRequestException("Connection refused.")));
        var service = provider.GetRequiredService<ICryptocurrencyService>();

        var result = await service.GetCoinAsync(new CoinQuery { CoinId = "bitcoin" }, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.UpstreamUnavailable);
        MeasurementsOf("cryptostuff.upstream.requests").Should().ContainSingle()
            .Which.Tags.Should().Contain("outcome", "no_response");
    }
}
