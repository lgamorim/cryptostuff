using System.Net;
using CryptoStuff.CoinGecko.UnitTests.TestSupport;

namespace CryptoStuff.CoinGecko.UnitTests;

public sealed class CoinGeckoClientMetricsTests : IDisposable
{
    private readonly RecordingMeterFactory _meterFactory = new();

    public void Dispose() => _meterFactory.Dispose();

    private sealed record TestPayload(string Name, int Value);

    private CoinGeckoClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }, _meterFactory);

    private static RecordedMeasurement Request(string endpoint, string outcome) => new(
        "cryptostuff.upstream",
        "cryptostuff.upstream.requests",
        "{request}",
        1,
        new Dictionary<string, object?> { ["provider"] = "coingecko", ["endpoint"] = endpoint, ["outcome"] = outcome });

    [Fact]
    public async Task Should_RecordOneSuccessfulRequest_When_ResponseIsSuccessful()
    {
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """{"name":"bitcoin","value":42}"""));

        await client.GetAsync<TestPayload>("coins/bitcoin", CancellationToken.None);

        _meterFactory.Measurements.Should().BeEquivalentTo([Request("coins/{id}", "success")]);
    }

    [Fact]
    public async Task Should_RecordNoRequestAndSendNothing_When_RequestUriIsNull()
    {
        var sendCount = 0;
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """{"name":"bitcoin","value":42}""", _ => sendCount++));

        // `!` is deliberate: the test hands over the null the guard must reject.
        var act = () => client.GetAsync<TestPayload>(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("requestUri");
        sendCount.Should().Be(0);
        _meterFactory.Measurements.Should().BeEmpty();
    }

    // Endpoint tag — every endpoint the adapter calls, through the extension that calls it.

    [Theory]
    [InlineData("simple/price")]
    [InlineData("simple/token_price/{platform}")]
    [InlineData("coins/{id}/market_chart")]
    [InlineData("coins/{id}")]
    [InlineData("coins/{id}/history")]
    public async Task Should_TagRequestWithEndpointTemplate_When_ExtensionCallsThatEndpoint(string endpoint)
    {
        // A JSON null body deserializes as success for every response type, whatever its required members.
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "null"));

        await CallEndpointAsync(client, endpoint);

        _meterFactory.Measurements.Should().BeEquivalentTo([Request(endpoint, "success")]);
    }

    [Theory]
    [InlineData("ping")]
    [InlineData("")]
    [InlineData("coins")]
    [InlineData("coins/bitcoin/tickers")]
    [InlineData("coins/bitcoin/history/extra")]
    [InlineData("simple/price/extra")]
    [InlineData("simple/token_price")]
    [InlineData("simple/supported_vs_currencies")]
    [InlineData("coins/")]
    [InlineData("coins//history")]
    [InlineData("simple/token_price/")]
    public async Task Should_TagRequestWithOtherEndpoint_When_RequestUriMatchesNoKnownTemplate(string requestUri)
    {
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "{}"));

        await client.GetAsync<Dictionary<string, object>>(requestUri, CancellationToken.None);

        _meterFactory.Measurements.Should().BeEquivalentTo([Request("other", "success")]);
    }

    // Outcome tag.

    [Theory]
    [InlineData(404, "not_found")]
    [InlineData(429, "rate_limited")]
    [InlineData(400, "client_error")]
    [InlineData(401, "client_error")]
    [InlineData(499, "client_error")]
    [InlineData(500, "server_error")]
    [InlineData(503, "server_error")]
    [InlineData(599, "server_error")]
    [InlineData(304, "other_status")]
    [InlineData(600, "other_status")]
    public async Task Should_TagRequestWithOutcomeForStatus_When_ResponseIsNotSuccessful(int statusCode, string outcome)
    {
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson((HttpStatusCode)statusCode, ""));

        await client.GetAsync<TestPayload>("coins/bitcoin", CancellationToken.None);

        _meterFactory.Measurements.Should().BeEquivalentTo([Request("coins/{id}", outcome)]);
    }

    [Fact]
    public async Task Should_TagRequestAsTimeout_When_HttpClientTimeoutElapses()
    {
        var client = CreateClient(FakeHttpMessageHandler.SimulatingTimeout());

        await client.GetAsync<TestPayload>("coins/bitcoin", CancellationToken.None);

        _meterFactory.Measurements.Should().BeEquivalentTo([Request("coins/{id}", "timeout")]);
    }

    [Fact]
    public async Task Should_TagRequestAsCancelledAndRethrow_When_CallerCancelsToken()
    {
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """{"name":"bitcoin","value":42}"""));
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var act = () => client.GetAsync<TestPayload>("coins/bitcoin", cancellationSource.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _meterFactory.Measurements.Should().BeEquivalentTo([Request("coins/{id}", "cancelled")]);
    }

    [Fact]
    public async Task Should_TagRequestAsNoResponse_When_SendFailsWithoutResponse()
    {
        var client = CreateClient(FakeHttpMessageHandler.Throwing(new HttpRequestException("Connection refused.")));

        await client.GetAsync<TestPayload>("coins/bitcoin", CancellationToken.None);

        _meterFactory.Measurements.Should().BeEquivalentTo([Request("coins/{id}", "no_response")]);
    }

    [Fact]
    public async Task Should_TagRequestAsCancelledAndRethrow_When_CallerCancelsAndSendFailsWithoutResponse()
    {
        var client = CreateClient(FakeHttpMessageHandler.ThrowingOnceCancelled(new HttpRequestException("Connection reset.")));
        using var cancellationSource = new CancellationTokenSource();

        var call = client.GetAsync<TestPayload>("coins/bitcoin", cancellationSource.Token);
        await cancellationSource.CancelAsync();
        var act = () => call;

        await act.Should().ThrowAsync<OperationCanceledException>();
        _meterFactory.Measurements.Should().BeEquivalentTo([Request("coins/{id}", "cancelled")]);
    }

    [Fact]
    public async Task Should_ReturnTimeoutTaggedTimeout_When_PendingRequestsAreCancelledAndSendFailsWithoutResponse()
    {
        // Stands in for HttpClient.Timeout, which cancels the send's linked token
        // the same way but only on a real timer that takes no TimeProvider.
        // CancelPendingRequests cancels that token with no clock, and the
        // caller's own token stays uncancelled, as it does on a timeout.
        using var httpClient = new HttpClient(FakeHttpMessageHandler.ThrowingOnceCancelled(new HttpRequestException("Connection reset.")))
        {
            BaseAddress = new Uri("https://example.test/"),
        };
        var client = new CoinGeckoClient(httpClient, _meterFactory);

        var call = client.GetAsync<TestPayload>("coins/bitcoin", CancellationToken.None);
        httpClient.CancelPendingRequests();
        var response = await call;

        response.IsTimeout.Should().BeTrue();
        _meterFactory.Measurements.Should().BeEquivalentTo([Request("coins/{id}", "timeout")]);
    }

    [Fact]
    public async Task Should_TagRequestAsExceptionAndRethrow_When_SuccessfulBodyIsNotValidJson()
    {
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "not json"));

        var act = () => client.GetAsync<TestPayload>("coins/bitcoin", CancellationToken.None);

        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
        _meterFactory.Measurements.Should().BeEquivalentTo([Request("coins/{id}", "exception")]);
    }

    private static Task CallEndpointAsync(ICoinGeckoClient client, string endpoint) => endpoint switch
    {
        "simple/price" => client.GetSimplePriceAsync(["bitcoin"], ["usd"], CancellationToken.None),
        "simple/token_price/{platform}" => client.GetSimpleTokenPriceAsync("ethereum", ["0xabc"], ["usd"], CancellationToken.None),
        "coins/{id}/market_chart" => client.GetMarketChartAsync("bitcoin", "usd", 7, CancellationToken.None),
        "coins/{id}" => client.GetCoinAsync("bitcoin", CancellationToken.None),
        "coins/{id}/history" => client.GetCoinHistoryAsync("bitcoin", "30-12-2024", CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, "Not an endpoint the adapter calls."),
    };
}
