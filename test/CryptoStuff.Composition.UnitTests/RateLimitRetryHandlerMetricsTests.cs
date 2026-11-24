using System.Net;
using CryptoStuff.Composition.UnitTests.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace CryptoStuff.Composition.UnitTests;

public sealed class RateLimitRetryHandlerMetricsTests : IDisposable
{
    private static readonly Uri RequestUri = new("https://example.test/coins/bitcoin");

    private readonly RecordingMeterFactory _meterFactory = new();

    public void Dispose() => _meterFactory.Dispose();

    private static readonly RecordedMeasurement Retry = new(
        "cryptostuff.upstream",
        "cryptostuff.upstream.retries",
        "{retry}",
        1,
        new Dictionary<string, object?> { ["provider"] = "coingecko" });

    private static HttpResponseMessage TooManyRequests() => new(HttpStatusCode.TooManyRequests);

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK);

    private async Task<HttpResponseMessage> SendAsync(SequencedHttpMessageHandler inner, int maxRetryAttempts)
    {
        using var sut = new RateLimitRetryHandler(maxRetryAttempts, TimeSpan.Zero, meterFactory: _meterFactory) { InnerHandler = inner };
        using var invoker = new HttpMessageInvoker(sut);
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestUri);
        return await invoker.SendAsync(request, CancellationToken.None);
    }

    [Fact]
    public async Task Should_RecordOneRetry_When_FirstAttemptIsRateLimitedAndSecondSucceeds()
    {
        var inner = SequencedHttpMessageHandler.ReturningInOrder(TooManyRequests, Ok);

        await SendAsync(inner, maxRetryAttempts: 3);

        _meterFactory.Measurements.Should().BeEquivalentTo([Retry]);
    }

    [Fact]
    public async Task Should_RecordEveryRetry_When_RetriesAreExhausted()
    {
        var inner = SequencedHttpMessageHandler.ReturningInOrder(TooManyRequests, TooManyRequests, TooManyRequests, TooManyRequests);

        var response = await SendAsync(inner, maxRetryAttempts: 3);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        inner.CallCount.Should().Be(4);
        _meterFactory.Measurements.Should().BeEquivalentTo([Retry, Retry, Retry]);
    }

    [Fact]
    public async Task Should_RecordNoRetry_When_FirstAttemptSucceeds()
    {
        var inner = SequencedHttpMessageHandler.ReturningInOrder(Ok);

        await SendAsync(inner, maxRetryAttempts: 3);

        _meterFactory.Measurements.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_RecordNoRetry_When_MaxRetryAttemptsIsZero()
    {
        var inner = SequencedHttpMessageHandler.ReturningInOrder(TooManyRequests);

        await SendAsync(inner, maxRetryAttempts: 0);

        _meterFactory.Measurements.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_RecordNoRetry_When_FailureIsNotTooManyRequests()
    {
        var inner = SequencedHttpMessageHandler.ReturningInOrder(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        await SendAsync(inner, maxRetryAttempts: 3);

        _meterFactory.Measurements.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_RecordNoRetry_When_CancelledDuringRetryDelay()
    {
        var inner = SequencedHttpMessageHandler.ReturningInOrder(TooManyRequests);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var sut = new RateLimitRetryHandler(maxRetryAttempts: 1, TimeSpan.FromSeconds(10), timeProvider, _meterFactory) { InnerHandler = inner };
        using var invoker = new HttpMessageInvoker(sut);
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestUri);
        using var cts = new CancellationTokenSource();

        var sendTask = invoker.SendAsync(request, cts.Token);
        await Task.Yield();
        await cts.CancelAsync();

        var act = async () => await sendTask;
        await act.Should().ThrowAsync<OperationCanceledException>();
        inner.CallCount.Should().Be(1);
        _meterFactory.Measurements.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_RecordRetryAndRethrow_When_ResendThrows()
    {
        var inner = SequencedHttpMessageHandler.ReturningInOrder(
            TooManyRequests,
            () => throw new HttpRequestException("Connection refused."));

        var act = () => SendAsync(inner, maxRetryAttempts: 3);

        await act.Should().ThrowAsync<HttpRequestException>();
        inner.CallCount.Should().Be(2);
        _meterFactory.Measurements.Should().BeEquivalentTo([Retry]);
    }

    [Fact]
    public async Task Should_RecordRetryAndRethrow_When_CallerCancelsDuringResend()
    {
        using var cts = new CancellationTokenSource();
        var inner = SequencedHttpMessageHandler.ReturningInOrder(
            TooManyRequests,
            () =>
            {
                // The caller cancels while the resend is in flight, after the delay is over.
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            });
        using var sut = new RateLimitRetryHandler(maxRetryAttempts: 3, TimeSpan.Zero, meterFactory: _meterFactory) { InnerHandler = inner };
        using var invoker = new HttpMessageInvoker(sut);
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestUri);

        var act = () => invoker.SendAsync(request, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        inner.CallCount.Should().Be(2);
        _meterFactory.Measurements.Should().BeEquivalentTo([Retry]);
    }
}
