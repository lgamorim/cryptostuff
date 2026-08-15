using System.Net;
using CryptoStuff.CoinGecko.UnitTests.TestSupport;

namespace CryptoStuff.CoinGecko.UnitTests;

public class CoinGeckoClientTests
{
    private sealed record TestPayload(string Name, int Value);

    [Fact]
    public async Task Should_ReturnDeserializedValue_When_ResponseIsSuccessful()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """{"name":"bitcoin","value":42}""");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetAsync<TestPayload>("coins/bitcoin", CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        response.IsTimeout.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Value.Should().Be(new TestPayload("bitcoin", 42));
    }

    [Fact]
    public async Task Should_CaptureStatusCode_When_ResponseIsNotSuccessful()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.NotFound, "");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetAsync<TestPayload>("coins/unknown", CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Value.Should().BeNull();
    }

    [Fact]
    public async Task Should_ReturnTimeoutResponse_When_HttpClientTimeoutElapses()
    {
        var handler = FakeHttpMessageHandler.SimulatingTimeout();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetAsync<TestPayload>("coins/bitcoin", CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeTrue();
        response.StatusCode.Should().BeNull();
    }

    [Fact]
    public async Task Should_PropagateCancellation_When_CallerCancelsToken()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """{"name":"bitcoin","value":42}""");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var act = () => client.GetAsync<TestPayload>("coins/bitcoin", cancellationSource.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
