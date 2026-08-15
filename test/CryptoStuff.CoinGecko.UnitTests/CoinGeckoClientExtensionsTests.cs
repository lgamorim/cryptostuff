using System.Net;
using System.Text.Json;
using CryptoStuff.CoinGecko.UnitTests.TestSupport;

namespace CryptoStuff.CoinGecko.UnitTests;

public class CoinGeckoClientExtensionsTests
{
    [Fact]
    public async Task Should_BuildCommaJoinedPercentEncodedRequestUri_When_RequestingSimplePrice()
    {
        Uri? capturedUri = null;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "{}", onRequest: r => capturedUri = r.RequestUri);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        await client.GetSimplePriceAsync(["bitcoin", "eth,ereum"], ["usd", "eur"], CancellationToken.None);

        var uri = capturedUri ?? throw new InvalidOperationException("Request was not captured.");
        uri.PathAndQuery.Should().Be("/simple/price?ids=bitcoin,eth%2Cereum&vs_currencies=usd,eur");
    }

    [Fact]
    public async Task Should_BuildEmptyQueryParameters_When_NoIdentifiersOrCurrenciesProvided()
    {
        Uri? capturedUri = null;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "{}", onRequest: r => capturedUri = r.RequestUri);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        await client.GetSimplePriceAsync([], [], CancellationToken.None);

        var uri = capturedUri ?? throw new InvalidOperationException("Request was not captured.");
        uri.PathAndQuery.Should().Be("/simple/price?ids=&vs_currencies=");
    }

    [Fact]
    public async Task Should_ReturnDeserializedPriceMatrix_When_ResponseIsSuccessful()
    {
        var json = """{"bitcoin":{"usd":43189.52,"eur":39750.10},"ethereum":{"usd":2280.14,"eur":2100.55}}""";
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetSimplePriceAsync(["bitcoin", "ethereum"], ["usd", "eur"], CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        response.Value.Should().HaveCount(2);
        var value = response.Value ?? throw new InvalidOperationException("Expected a value.");
        value["bitcoin"]["usd"].Should().Be(43189.52m);
    }

    [Fact]
    public async Task Should_ReturnEmptyPriceMatrix_When_ResponseBodyIsEmptyJsonObject()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "{}");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetSimplePriceAsync(["unknown-coin"], ["usd"], CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        response.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ThrowJsonException_When_ResponseBodyIsMalformedJson()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "{not valid json");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var act = () => client.GetSimplePriceAsync(["bitcoin"], ["usd"], CancellationToken.None);

        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task Should_CaptureStatusCode_When_ResponseIsNotSuccessful()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.TooManyRequests, "");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetSimplePriceAsync(["bitcoin"], ["usd"], CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Should_ReturnTimeoutResponse_When_HttpClientTimeoutElapses()
    {
        var handler = FakeHttpMessageHandler.SimulatingTimeout();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetSimplePriceAsync(["bitcoin"], ["usd"], CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeTrue();
        response.StatusCode.Should().BeNull();
    }

    [Fact]
    public async Task Should_BuildPercentEncodedPlatformSegmentAndCommaJoinedRequestUri_When_RequestingSimpleTokenPrice()
    {
        Uri? capturedUri = null;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "{}", onRequest: r => capturedUri = r.RequestUri);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        await client.GetSimpleTokenPriceAsync("polygon pos", ["0xAbC", "0xDeF,123"], ["usd"], CancellationToken.None);

        var uri = capturedUri ?? throw new InvalidOperationException("Request was not captured.");
        uri.PathAndQuery.Should().Be("/simple/token_price/polygon%20pos?contract_addresses=0xAbC,0xDeF%2C123&vs_currencies=usd");
    }

    [Fact]
    public async Task Should_ReturnDeserializedPriceMatrix_When_RequestingSimpleTokenPriceAndResponseIsSuccessful()
    {
        var json = """{"0xabc":{"usd":1.23}}""";
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetSimpleTokenPriceAsync("ethereum", ["0xabc"], ["usd"], CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        var value = response.Value ?? throw new InvalidOperationException("Expected a value.");
        value["0xabc"]["usd"].Should().Be(1.23m);
    }
}
