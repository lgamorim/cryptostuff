using System.Net;
using System.Text.Json;
using CryptoStuff.CoinMarketCap.UnitTests.TestSupport;

namespace CryptoStuff.CoinMarketCap.UnitTests;

public sealed class CoinMarketCapClientMetricsTests : IDisposable
{
    private readonly RecordingMeterFactory _meterFactory = new();

    public void Dispose() => _meterFactory.Dispose();

    private sealed record TestPayload(string Name, int Value);

    private CoinMarketCapClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }, _meterFactory);

    private static RecordedMeasurement Request(string endpoint, string outcome) => new(
        "cryptostuff.upstream",
        "cryptostuff.upstream.requests",
        "{request}",
        1,
        new Dictionary<string, object?> { ["provider"] = "coinmarketcap", ["endpoint"] = endpoint, ["outcome"] = outcome });

    [Fact]
    public async Task Should_RecordOneSuccessfulRequest_When_ResponseIsSuccessful()
    {
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """{"data":{"name":"bitcoin","value":42}}"""));

        await client.GetAsync<TestPayload>("v2/cryptocurrency/info?slug=bitcoin", CancellationToken.None);

        _meterFactory.Measurements.Should().BeEquivalentTo([Request("v2/cryptocurrency/info", "success")]);
    }

    [Fact]
    public async Task Should_RecordNoRequestAndSendNothing_When_RequestUriIsNull()
    {
        var sendCount = 0;
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "null", _ => sendCount++));

        // `!` is deliberate: the test hands over the null the guard must reject.
        var act = () => client.GetAsync<TestPayload>(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("requestUri");
        sendCount.Should().Be(0);
        _meterFactory.Measurements.Should().BeEmpty();
    }

    // Endpoint tag — every call the extensions make, through the extension that makes it.

    [Theory]
    [InlineData("latest quotes", "v3/cryptocurrency/quotes/latest")]
    [InlineData("info by slug", "v2/cryptocurrency/info")]
    [InlineData("info by address", "v2/cryptocurrency/info")]
    [InlineData("historical quotes", "v3/cryptocurrency/quotes/historical")]
    public async Task Should_TagRequestWithEndpointPath_When_ExtensionCallsThatEndpoint(string call, string endpoint)
    {
        // A JSON null body deserializes as success for every response type, whatever its required members.
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "null"));

        await CallAsync(client, call);

        _meterFactory.Measurements.Should().BeEquivalentTo([Request(endpoint, "success")]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("v1/cryptocurrency/map")]
    [InlineData("v2/cryptocurrency/quotes/latest?slug=bitcoin")]
    [InlineData("v3/cryptocurrency/quotes/latest/extra")]
    [InlineData("v3/cryptocurrency/info")]
    [InlineData("cryptocurrency/info")]
    [InlineData("/v2/cryptocurrency/info")]
    [InlineData("V2/cryptocurrency/info")]
    public async Task Should_TagRequestWithOtherEndpoint_When_RequestUriMatchesNoKnownPath(string requestUri)
    {
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "null"));

        await client.GetAsync<TestPayload>(requestUri, CancellationToken.None);

        _meterFactory.Measurements.Should().BeEquivalentTo([Request("other", "success")]);
    }

    // Outcome tag.

    [Theory]
    [InlineData(404, "not_found")]
    [InlineData(429, "rate_limited")]
    [InlineData(400, "client_error")]
    [InlineData(401, "client_error")]
    [InlineData(402, "client_error")]
    [InlineData(403, "client_error")]
    [InlineData(499, "client_error")]
    [InlineData(500, "server_error")]
    [InlineData(503, "server_error")]
    [InlineData(599, "server_error")]
    [InlineData(304, "other_status")]
    [InlineData(600, "other_status")]
    public async Task Should_TagRequestWithOutcomeForStatus_When_ResponseIsNotSuccessful(int statusCode, string outcome)
    {
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson((HttpStatusCode)statusCode, ""));

        await client.GetAsync<TestPayload>("v2/cryptocurrency/info?slug=bitcoin", CancellationToken.None);

        _meterFactory.Measurements.Should().BeEquivalentTo([Request("v2/cryptocurrency/info", outcome)]);
    }

    [Fact]
    public async Task Should_TagRequestAsTimeout_When_HttpClientTimeoutElapses()
    {
        var client = CreateClient(FakeHttpMessageHandler.SimulatingTimeout());

        await client.GetAsync<TestPayload>("v2/cryptocurrency/info?slug=bitcoin", CancellationToken.None);

        _meterFactory.Measurements.Should().BeEquivalentTo([Request("v2/cryptocurrency/info", "timeout")]);
    }

    [Fact]
    public async Task Should_TagRequestAsCancelledAndRethrow_When_CallerCancelsToken()
    {
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "null"));
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var act = () => client.GetAsync<TestPayload>("v2/cryptocurrency/info?slug=bitcoin", cancellationSource.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _meterFactory.Measurements.Should().BeEquivalentTo([Request("v2/cryptocurrency/info", "cancelled")]);
    }

    [Fact]
    public async Task Should_TagRequestAsNoResponse_When_SendFailsWithoutResponse()
    {
        var client = CreateClient(FakeHttpMessageHandler.Throwing(new HttpRequestException("Connection refused.")));

        await client.GetAsync<TestPayload>("v2/cryptocurrency/info?slug=bitcoin", CancellationToken.None);

        _meterFactory.Measurements.Should().BeEquivalentTo([Request("v2/cryptocurrency/info", "no_response")]);
    }

    [Fact]
    public async Task Should_TagRequestAsCancelledAndRethrow_When_CallerCancelsAndSendFailsWithoutResponse()
    {
        var client = CreateClient(FakeHttpMessageHandler.ThrowingOnceCancelled(new HttpRequestException("Connection reset.")));
        using var cancellationSource = new CancellationTokenSource();

        var call = client.GetAsync<TestPayload>("v2/cryptocurrency/info?slug=bitcoin", cancellationSource.Token);
        await cancellationSource.CancelAsync();
        var act = () => call;

        await act.Should().ThrowAsync<OperationCanceledException>();
        _meterFactory.Measurements.Should().BeEquivalentTo([Request("v2/cryptocurrency/info", "cancelled")]);
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
        var client = new CoinMarketCapClient(httpClient, _meterFactory);

        var call = client.GetAsync<TestPayload>("v2/cryptocurrency/info?slug=bitcoin", CancellationToken.None);
        httpClient.CancelPendingRequests();
        var response = await call;

        response.IsTimeout.Should().BeTrue();
        _meterFactory.Measurements.Should().BeEquivalentTo([Request("v2/cryptocurrency/info", "timeout")]);
    }

    [Fact]
    public async Task Should_TagRequestAsExceptionAndRethrow_When_SuccessfulBodyIsNotValidJson()
    {
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "not json"));

        var act = () => client.GetAsync<TestPayload>("v2/cryptocurrency/info?slug=bitcoin", CancellationToken.None);

        await act.Should().ThrowAsync<JsonException>();
        _meterFactory.Measurements.Should().BeEquivalentTo([Request("v2/cryptocurrency/info", "exception")]);
    }

    private static Task CallAsync(ICoinMarketCapClient client, string call) => call switch
    {
        "latest quotes" => client.GetLatestQuotesAsync(["bitcoin"], ["USD"], CancellationToken.None),
        "info by slug" => client.GetInfoBySlugAsync(["bitcoin"], CancellationToken.None),
        "info by address" => client.GetInfoByAddressAsync("0xabc", CancellationToken.None),
        "historical quotes" => client.GetHistoricalQuotesAsync(1, "USD", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(call), call, "Not a call the extensions make."),
    };
}
