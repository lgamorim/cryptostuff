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

    [Fact]
    public async Task Should_BuildPercentEncodedCoinIdAndQueryString_When_RequestingMarketChart()
    {
        Uri? capturedUri = null;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, EmptyMarketChartJson, onRequest: r => capturedUri = r.RequestUri);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        await client.GetMarketChartAsync("bit coin", "u s d", 30, CancellationToken.None);

        var uri = capturedUri ?? throw new InvalidOperationException("Request was not captured.");
        uri.PathAndQuery.Should().Be("/coins/bit%20coin/market_chart?vs_currency=u%20s%20d&days=30");
    }

    [Fact]
    public async Task Should_ReturnDeserializedMarketChart_When_ResponseIsSuccessful()
    {
        var json = """
            {
                "prices": [[1700000000000, 43189.52], [1700003600000, 43200.11]],
                "market_caps": [[1700000000000, 850000000000]],
                "total_volumes": [[1700000000000, 25000000000]]
            }
            """;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetMarketChartAsync("bitcoin", "usd", 30, CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        var value = response.Value ?? throw new InvalidOperationException("Expected a value.");
        value.Prices.Should().HaveCount(2);
        value.Prices[0].Should().Be(new CoinGeckoMarketChartPoint(1700000000000, 43189.52m));
        value.MarketCaps.Should().ContainSingle().Which.Should().Be(new CoinGeckoMarketChartPoint(1700000000000, 850000000000m));
        value.TotalVolumes.Should().ContainSingle().Which.Should().Be(new CoinGeckoMarketChartPoint(1700000000000, 25000000000m));
    }

    [Fact]
    public async Task Should_ReturnEmptySeries_When_ResponseArraysAreEmpty()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, EmptyMarketChartJson);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetMarketChartAsync("bitcoin", "usd", 30, CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        var value = response.Value ?? throw new InvalidOperationException("Expected a value.");
        value.Prices.Should().BeEmpty();
        value.MarketCaps.Should().BeEmpty();
        value.TotalVolumes.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ThrowJsonException_When_MarketChartResponseIsMissingASeries()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """{"prices":[],"total_volumes":[]}""");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var act = () => client.GetMarketChartAsync("bitcoin", "usd", 30, CancellationToken.None);

        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task Should_ThrowJsonException_When_MarketChartResponseBodyIsMalformedJson()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "{not valid json");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var act = () => client.GetMarketChartAsync("bitcoin", "usd", 30, CancellationToken.None);

        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task Should_CaptureStatusCode_When_MarketChartResponseIsNotSuccessful()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.NotFound, "");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetMarketChartAsync("unknown-coin", "usd", 30, CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnTimeoutResponse_When_MarketChartHttpClientTimeoutElapses()
    {
        var handler = FakeHttpMessageHandler.SimulatingTimeout();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetMarketChartAsync("bitcoin", "usd", 30, CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeTrue();
        response.StatusCode.Should().BeNull();
    }

    private const string EmptyMarketChartJson = """{"prices":[],"market_caps":[],"total_volumes":[]}""";
}
