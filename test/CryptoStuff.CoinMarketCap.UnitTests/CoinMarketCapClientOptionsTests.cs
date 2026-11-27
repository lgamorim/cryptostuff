using System.Net;
using CryptoStuff.CoinMarketCap.UnitTests.TestSupport;

namespace CryptoStuff.CoinMarketCap.UnitTests;

public class CoinMarketCapClientOptionsTests
{
    [Fact]
    public void Should_UseDefaultBaseAddress_When_NotOverridden()
    {
        var options = new CoinMarketCapClientOptions { ApiKey = "key" };
        using var httpClient = new HttpClient();

        options.ApplyTo(httpClient);

        httpClient.BaseAddress.Should().Be(new Uri("https://pro-api.coinmarketcap.com/"));
    }

    [Fact]
    public void Should_OverrideBaseAddress_When_ExplicitlyProvided()
    {
        var customBaseAddress = new Uri("https://example.test/api/");
        var options = new CoinMarketCapClientOptions { ApiKey = "key", BaseAddress = customBaseAddress };
        using var httpClient = new HttpClient();

        options.ApplyTo(httpClient);

        httpClient.BaseAddress.Should().Be(customBaseAddress);
    }

    [Fact]
    public void Should_AddApiKeyHeader_When_Applied()
    {
        var options = new CoinMarketCapClientOptions { ApiKey = "secret-key" };
        using var httpClient = new HttpClient();

        options.ApplyTo(httpClient);

        httpClient.DefaultRequestHeaders.GetValues("X-CMC_PRO_API_KEY")
            .Should().ContainSingle().Which.Should().Be("secret-key");
    }

    [Fact]
    public async Task Should_SendApiKeyHeaderAndResolveUnderBaseAddress_When_RequestSentThroughConfiguredClient()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "{}", onRequest: request => capturedRequest = request);
        using var httpClient = new HttpClient(handler);
        new CoinMarketCapClientOptions { ApiKey = "secret-key" }.ApplyTo(httpClient);
        ICoinMarketCapClient client = new CoinMarketCapClient(httpClient);

        await client.GetAsync<object>("v3/cryptocurrency/quotes/latest", CancellationToken.None);

        var request = capturedRequest ?? throw new InvalidOperationException("Request was not captured.");
        // `!` is safe: a sent request always has its URI resolved.
        request.RequestUri!.AbsoluteUri.Should().Be("https://pro-api.coinmarketcap.com/v3/cryptocurrency/quotes/latest");
        request.Headers.GetValues("X-CMC_PRO_API_KEY").Should().ContainSingle().Which.Should().Be("secret-key");
    }
}
