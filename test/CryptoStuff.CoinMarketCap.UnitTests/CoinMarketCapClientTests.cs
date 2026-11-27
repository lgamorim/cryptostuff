using System.Net;
using System.Text.Json;
using CryptoStuff.CoinMarketCap.UnitTests.TestSupport;

namespace CryptoStuff.CoinMarketCap.UnitTests;

public class CoinMarketCapClientTests
{
    private sealed record TestPayload(string Name, int Value);

    private static ICoinMarketCapClient CreateClient(HttpMessageHandler handler) =>
        new CoinMarketCapClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });

    // Success.

    [Fact]
    public async Task Should_ReturnTheDeserializedData_When_ResponseIsSuccessful()
    {
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(
            HttpStatusCode.OK,
            """{"status":{"timestamp":"2024-01-31T00:00:00.000Z","error_code":0,"error_message":null,"elapsed":10,"credit_count":1},"data":{"name":"bitcoin","value":42}}"""));

        var response = await client.GetAsync<TestPayload>("v2/cryptocurrency/info", CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        response.IsTimeout.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Value.Should().Be(new TestPayload("bitcoin", 42));
        response.ErrorCode.Should().BeNull();
        response.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("""{"status":{"error_code":0,"error_message":null}}""")]
    [InlineData("""{"status":{"error_code":0,"error_message":null},"data":null}""")]
    public async Task Should_ReturnSuccessWithoutValue_When_SuccessfulBodyCarriesNoData(string body)
    {
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, body));

        var response = await client.GetAsync<TestPayload>("v2/cryptocurrency/info", CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        response.Value.Should().BeNull();
    }

    [Theory]
    [InlineData("""{"status":{"error_code":"","error_message":null},"data":{"name":"bitcoin","value":42}}""")]
    [InlineData("""{"status":{"error_code":0,"error_message":7},"data":{"name":"bitcoin","value":42}}""")]
    [InlineData("""{"status":"unexpected","data":{"name":"bitcoin","value":42}}""")]
    public async Task Should_ReturnTheData_When_SuccessfulBodysStatusIsUnreadable(string body)
    {
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, body));

        var response = await client.GetAsync<TestPayload>("v2/cryptocurrency/info", CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        response.Value.Should().Be(new TestPayload("bitcoin", 42));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    public async Task Should_ThrowJsonException_When_SuccessfulBodyIsNotValidJson(string body)
    {
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, body));

        var act = () => client.GetAsync<TestPayload>("v2/cryptocurrency/info", CancellationToken.None);

        await act.Should().ThrowAsync<JsonException>();
    }

    // Failure.

    [Fact]
    public async Task Should_CaptureStatusCodeAndCoinMarketCapError_When_ResponseIsNotSuccessful()
    {
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(
            HttpStatusCode.TooManyRequests,
            """{"status":{"timestamp":"2024-01-31T00:00:00.000Z","error_code":1008,"error_message":"You've exceeded your API Key's HTTP request rate limit.","elapsed":0,"credit_count":0}}"""));

        var response = await client.GetAsync<TestPayload>("v3/cryptocurrency/quotes/latest", CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Value.Should().BeNull();
        response.ErrorCode.Should().Be(1008);
        response.ErrorMessage.Should().Be("You've exceeded your API Key's HTTP request rate limit.");
    }

    [Fact]
    public async Task Should_ReadTheErrorCode_When_FailureBodyCarriesItAsAString()
    {
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(
            HttpStatusCode.BadRequest,
            """{"status":{"error_code":"400","error_message":"Invalid value for \"slug\": \"not-a-coin\""}}"""));

        var response = await client.GetAsync<TestPayload>("v3/cryptocurrency/quotes/latest", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.ErrorCode.Should().Be(400);
        response.ErrorMessage.Should().Be("Invalid value for \"slug\": \"not-a-coin\"");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("<html><body>Bad Gateway</body></html>")]
    [InlineData("null")]
    [InlineData("""{"message":"no status here"}""")]
    [InlineData("""{"status":null}""")]
    public async Task Should_CaptureStatusCodeWithoutCoinMarketCapError_When_FailureBodyCarriesNoStatus(string body)
    {
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(HttpStatusCode.BadGateway, body));

        var response = await client.GetAsync<TestPayload>("v3/cryptocurrency/quotes/latest", CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        response.Value.Should().BeNull();
        response.ErrorCode.Should().BeNull();
        response.ErrorMessage.Should().BeNull();
    }

    // No response.

    [Fact]
    public async Task Should_ReturnTimeoutResponse_When_HttpClientTimeoutElapses()
    {
        var client = CreateClient(FakeHttpMessageHandler.SimulatingTimeout());

        var response = await client.GetAsync<TestPayload>("v2/cryptocurrency/info", CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeTrue();
        response.StatusCode.Should().BeNull();
    }

    [Fact]
    public async Task Should_ReturnNoResponse_When_SendFailsWithoutResponse()
    {
        var client = CreateClient(FakeHttpMessageHandler.Throwing(new HttpRequestException("Connection refused.")));

        var response = await client.GetAsync<TestPayload>("v2/cryptocurrency/info", CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeFalse();
        response.StatusCode.Should().BeNull();
        response.Value.Should().BeNull();
    }

    [Fact]
    public async Task Should_PropagateCancellation_When_CallerCancelsToken()
    {
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """{"data":{"name":"bitcoin","value":42}}"""));
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var act = () => client.GetAsync<TestPayload>("v2/cryptocurrency/info", cancellationSource.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_PropagateCancellation_When_CallerCancelsAndSendFailsWithoutResponse()
    {
        var client = CreateClient(FakeHttpMessageHandler.ThrowingOnceCancelled(new HttpRequestException("Connection reset.")));
        using var cancellationSource = new CancellationTokenSource();

        var call = client.GetAsync<TestPayload>("v2/cryptocurrency/info", cancellationSource.Token);
        await cancellationSource.CancelAsync();
        var act = () => call;

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ThrowArgumentNullExceptionAndSendNothing_When_RequestUriIsNull()
    {
        var sendCount = 0;
        var client = CreateClient(FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "null", _ => sendCount++));

        // `!` is deliberate: the test hands over the null the guard must reject.
        var act = () => client.GetAsync<TestPayload>(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("requestUri");
        sendCount.Should().Be(0);
    }
}
