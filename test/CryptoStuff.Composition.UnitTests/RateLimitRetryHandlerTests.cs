using System.Net;
using System.Net.Http.Headers;
using CryptoStuff.Composition.UnitTests.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace CryptoStuff.Composition.UnitTests;

public class RateLimitRetryHandlerTests
{
    private static readonly Uri RequestUri = new("https://example.test/coins/bitcoin");

    // Constructor validation.

    [Fact]
    public void Should_ThrowArgumentOutOfRangeException_When_MaxRetryAttemptsIsNegative()
    {
        var act = () => new RateLimitRetryHandler(maxRetryAttempts: -1, TimeSpan.FromSeconds(1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Should_ThrowArgumentOutOfRangeException_When_ConstantBackoffIsNegative()
    {
        var act = () => new RateLimitRetryHandler(maxRetryAttempts: 1, TimeSpan.FromSeconds(-1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Should_SendExactlyOnce_When_MaxRetryAttemptsIsZero()
    {
        var handler = SequencedHttpMessageHandler.ReturningInOrder(() => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        using var sut = new RateLimitRetryHandler(maxRetryAttempts: 0, TimeSpan.FromSeconds(1)) { InnerHandler = handler };
        using var invoker = new HttpMessageInvoker(sut);
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestUri);

        var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_RetryWithNoWait_When_ConstantBackoffIsZero()
    {
        var handler = SequencedHttpMessageHandler.ReturningInOrder(
            () => new HttpResponseMessage(HttpStatusCode.TooManyRequests),
            () => new HttpResponseMessage(HttpStatusCode.OK));
        using var sut = new RateLimitRetryHandler(maxRetryAttempts: 1, TimeSpan.Zero) { InnerHandler = handler };
        using var invoker = new HttpMessageInvoker(sut);
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestUri);

        var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.CallCount.Should().Be(2);
    }

    // Passthrough.

    [Fact]
    public async Task Should_ReturnResponse_When_FirstAttemptSucceeds()
    {
        var handler = SequencedHttpMessageHandler.ReturningInOrder(() => new HttpResponseMessage(HttpStatusCode.OK));
        using var sut = new RateLimitRetryHandler(maxRetryAttempts: 1, TimeSpan.FromSeconds(1)) { InnerHandler = handler };
        using var invoker = new HttpMessageInvoker(sut);
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestUri);

        var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_ReturnResponseUnchanged_When_StatusCodeIsNotTooManyRequests()
    {
        var handler = SequencedHttpMessageHandler.ReturningInOrder(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var sut = new RateLimitRetryHandler(maxRetryAttempts: 3, TimeSpan.FromSeconds(1)) { InnerHandler = handler };
        using var invoker = new HttpMessageInvoker(sut);
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestUri);

        var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        handler.CallCount.Should().Be(1);
    }

    // Retry / bound.

    [Fact]
    public async Task Should_RetryAndReturnSuccess_When_FirstAttemptIsTooManyRequestsAndSecondSucceeds()
    {
        var handler = SequencedHttpMessageHandler.ReturningInOrder(
            () => new HttpResponseMessage(HttpStatusCode.TooManyRequests),
            () => new HttpResponseMessage(HttpStatusCode.OK));
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var sut = new RateLimitRetryHandler(maxRetryAttempts: 1, TimeSpan.FromSeconds(1), timeProvider) { InnerHandler = handler };
        using var invoker = new HttpMessageInvoker(sut);
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestUri);

        var sendTask = invoker.SendAsync(request, CancellationToken.None);
        await Task.Yield();
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        var response = await sendTask;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Should_ReturnLastFailureResponseAfterExhaustingRetries_When_AllAttemptsAreTooManyRequests()
    {
        HttpResponseMessage? lastResponse = null;
        var handler = SequencedHttpMessageHandler.ReturningInOrder(
            () => new HttpResponseMessage(HttpStatusCode.TooManyRequests),
            () => new HttpResponseMessage(HttpStatusCode.TooManyRequests),
            () => lastResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var sut = new RateLimitRetryHandler(maxRetryAttempts: 2, TimeSpan.FromSeconds(1), timeProvider) { InnerHandler = handler };
        using var invoker = new HttpMessageInvoker(sut);
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestUri);

        var sendTask = invoker.SendAsync(request, CancellationToken.None);
        await Task.Yield();
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        await Task.Yield();
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        var response = await sendTask;

        response.Should().BeSameAs(lastResponse);
        handler.CallCount.Should().Be(3);
    }

    // Retry-After handling.

    [Fact]
    public async Task Should_WaitExactlyRetryAfterDeltaSeconds_When_RetryAfterHeaderIsDeltaForm()
    {
        var handler = SequencedHttpMessageHandler.ReturningInOrder(
            () => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(5)) },
            },
            () => new HttpResponseMessage(HttpStatusCode.OK));
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var sut = new RateLimitRetryHandler(maxRetryAttempts: 1, TimeSpan.FromSeconds(1), timeProvider) { InnerHandler = handler };
        using var invoker = new HttpMessageInvoker(sut);
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestUri);

        var sendTask = invoker.SendAsync(request, CancellationToken.None);
        await Task.Yield();
        handler.CallCount.Should().Be(1);

        timeProvider.Advance(TimeSpan.FromSeconds(5) - TimeSpan.FromMilliseconds(1));
        await Task.Yield();
        handler.CallCount.Should().Be(1);

        timeProvider.Advance(TimeSpan.FromMilliseconds(1));
        await sendTask;
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Should_WaitExactlyUntilRetryAfterDate_When_RetryAfterHeaderIsHttpDateForm()
    {
        var now = DateTimeOffset.UtcNow;
        var retryAfterDate = now.AddSeconds(3);
        var handler = SequencedHttpMessageHandler.ReturningInOrder(
            () => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Headers = { RetryAfter = new RetryConditionHeaderValue(retryAfterDate) },
            },
            () => new HttpResponseMessage(HttpStatusCode.OK));
        var timeProvider = new FakeTimeProvider(now);
        using var sut = new RateLimitRetryHandler(maxRetryAttempts: 1, TimeSpan.FromSeconds(1), timeProvider) { InnerHandler = handler };
        using var invoker = new HttpMessageInvoker(sut);
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestUri);

        var sendTask = invoker.SendAsync(request, CancellationToken.None);
        await Task.Yield();

        timeProvider.Advance(TimeSpan.FromSeconds(3) - TimeSpan.FromMilliseconds(1));
        await Task.Yield();
        handler.CallCount.Should().Be(1);

        timeProvider.Advance(TimeSpan.FromMilliseconds(1));
        await sendTask;
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Should_TreatPastRetryAfterDateAsNoWait_When_RetryAfterDateIsInThePast()
    {
        var now = DateTimeOffset.UtcNow;
        var handler = SequencedHttpMessageHandler.ReturningInOrder(
            () => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Headers = { RetryAfter = new RetryConditionHeaderValue(now.AddSeconds(-10)) },
            },
            () => new HttpResponseMessage(HttpStatusCode.OK));
        var timeProvider = new FakeTimeProvider(now);
        using var sut = new RateLimitRetryHandler(maxRetryAttempts: 1, TimeSpan.FromSeconds(1), timeProvider) { InnerHandler = handler };
        using var invoker = new HttpMessageInvoker(sut);
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestUri);

        var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Should_FallBackToConstantBackoff_When_RetryAfterHeaderIsAbsent()
    {
        var handler = SequencedHttpMessageHandler.ReturningInOrder(
            () => new HttpResponseMessage(HttpStatusCode.TooManyRequests),
            () => new HttpResponseMessage(HttpStatusCode.OK));
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var constantBackoff = TimeSpan.FromSeconds(2);
        using var sut = new RateLimitRetryHandler(maxRetryAttempts: 1, constantBackoff, timeProvider) { InnerHandler = handler };
        using var invoker = new HttpMessageInvoker(sut);
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestUri);

        var sendTask = invoker.SendAsync(request, CancellationToken.None);
        await Task.Yield();

        timeProvider.Advance(constantBackoff - TimeSpan.FromMilliseconds(1));
        await Task.Yield();
        handler.CallCount.Should().Be(1);

        timeProvider.Advance(TimeSpan.FromMilliseconds(1));
        await sendTask;
        handler.CallCount.Should().Be(2);
    }

    // Cancellation.

    [Fact]
    public async Task Should_ForwardCancellationToken_When_SendingToInnerHandler()
    {
        var handler = SequencedHttpMessageHandler.ReturningInOrder(() => new HttpResponseMessage(HttpStatusCode.OK));
        using var sut = new RateLimitRetryHandler(maxRetryAttempts: 1, TimeSpan.FromSeconds(1)) { InnerHandler = handler };
        using var invoker = new HttpMessageInvoker(sut);
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestUri);
        using var cts = new CancellationTokenSource();

        await invoker.SendAsync(request, cts.Token);

        handler.LastCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Should_StopRetryingAndThrow_When_CancellationTokenIsCancelledDuringRetryDelay()
    {
        var handler = SequencedHttpMessageHandler.ReturningInOrder(() => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var sut = new RateLimitRetryHandler(maxRetryAttempts: 1, TimeSpan.FromSeconds(10), timeProvider) { InnerHandler = handler };
        using var invoker = new HttpMessageInvoker(sut);
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestUri);
        using var cts = new CancellationTokenSource();

        var sendTask = invoker.SendAsync(request, cts.Token);
        await Task.Yield();
        await cts.CancelAsync();

        var act = async () => await sendTask;
        await act.Should().ThrowAsync<OperationCanceledException>();
        handler.CallCount.Should().Be(1);
    }

    // Request cloning.

    [Fact]
    public async Task Should_PreserveMethodUriAndHeaders_When_RetryingRequest()
    {
        var handler = SequencedHttpMessageHandler.ReturningInOrder(
            () => new HttpResponseMessage(HttpStatusCode.TooManyRequests),
            () => new HttpResponseMessage(HttpStatusCode.OK));
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var sut = new RateLimitRetryHandler(maxRetryAttempts: 1, TimeSpan.FromSeconds(1), timeProvider) { InnerHandler = handler };
        using var invoker = new HttpMessageInvoker(sut);
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestUri);
        request.Headers.Add("X-Test", "abc");

        var sendTask = invoker.SendAsync(request, CancellationToken.None);
        await Task.Yield();
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        await sendTask;

        handler.Requests.Should().HaveCount(2);
        handler.Requests[1].Should().NotBeSameAs(handler.Requests[0]);
        handler.Requests[1].Method.Should().Be(HttpMethod.Get);
        handler.Requests[1].RequestUri.Should().Be(RequestUri);
        handler.Requests[1].Headers.GetValues("X-Test").Should().ContainSingle().Which.Should().Be("abc");
    }
}
