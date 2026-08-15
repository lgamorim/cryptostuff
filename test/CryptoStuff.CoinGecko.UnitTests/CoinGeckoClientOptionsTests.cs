using System.Net;
using CryptoStuff.CoinGecko.UnitTests.TestSupport;

namespace CryptoStuff.CoinGecko.UnitTests;

public class CoinGeckoClientOptionsTests
{
    [Fact]
    public void Should_UseDefaultBaseAddress_When_NotOverridden()
    {
        var options = new CoinGeckoClientOptions { ApiKey = "key" };
        using var httpClient = new HttpClient();

        options.ApplyTo(httpClient);

        httpClient.BaseAddress.Should().Be(new Uri(CoinGeckoClientOptions.DefaultBaseAddress));
    }

    [Fact]
    public void Should_OverrideBaseAddress_When_ExplicitlyProvided()
    {
        var customBaseAddress = new Uri("https://example.test/api/");
        var options = new CoinGeckoClientOptions { ApiKey = "key", BaseAddress = customBaseAddress };
        using var httpClient = new HttpClient();

        options.ApplyTo(httpClient);

        httpClient.BaseAddress.Should().Be(customBaseAddress);
    }

    [Fact]
    public void Should_AddApiKeyHeader_When_Applied()
    {
        var options = new CoinGeckoClientOptions { ApiKey = "secret-key" };
        using var httpClient = new HttpClient();

        options.ApplyTo(httpClient);

        httpClient.DefaultRequestHeaders.GetValues(CoinGeckoClientOptions.ApiKeyHeaderName)
            .Should().ContainSingle().Which.Should().Be("secret-key");
    }

    [Fact]
    public async Task Should_ResolveRelativeRequestUriUnderApiVersionSegment_When_UsingDefaultBaseAddress()
    {
        Uri? capturedUri = null;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "{}", onRequest: r => capturedUri = r.RequestUri);
        using var httpClient = new HttpClient(handler);
        var options = new CoinGeckoClientOptions { ApiKey = "key" };
        options.ApplyTo(httpClient);
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        await client.GetAsync<object>("simple/price", CancellationToken.None);

        var uri = capturedUri ?? throw new InvalidOperationException("Request was not captured.");
        uri.AbsoluteUri.Should().Be("https://api.coingecko.com/api/v3/simple/price");
    }
}
