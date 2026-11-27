using System.Net;
using System.Text;

namespace CryptoStuff.CoinMarketCap.UnitTests.TestSupport;

/// <summary>
/// A stand-in <see cref="HttpMessageHandler"/> that never performs real
/// network I/O. Copied from CryptoStuff.CoinGecko.UnitTests rather than shared.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpResponseMessage>? _responseFactory;
    private readonly Action<HttpRequestMessage>? _onRequest;
    private readonly Func<CancellationToken, Task<HttpResponseMessage>>? _send;

    private FakeHttpMessageHandler(Func<HttpResponseMessage>? responseFactory, Action<HttpRequestMessage>? onRequest = null)
    {
        _responseFactory = responseFactory;
        _onRequest = onRequest;
    }

    private FakeHttpMessageHandler(Func<CancellationToken, Task<HttpResponseMessage>> send) => _send = send;

    /// <summary>
    /// Creates a handler that returns the given status code and JSON body.
    /// <paramref name="onRequest"/>, when given, is invoked with the request
    /// sent, so a test can assert on the request URI.
    /// </summary>
    public static FakeHttpMessageHandler ReturningJson(HttpStatusCode statusCode, string json, Action<HttpRequestMessage>? onRequest = null) =>
        new(() => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        }, onRequest);

    /// <summary>
    /// Creates a handler that reproduces an <see cref="HttpClient.Timeout"/> expiry:
    /// it throws <see cref="TaskCanceledException"/> unconditionally, without
    /// cancelling the caller's own <see cref="CancellationToken"/> — exactly
    /// what a real timeout does, since it cancels an internal linked token
    /// distinct from the caller's.
    /// </summary>
    public static FakeHttpMessageHandler SimulatingTimeout() => new(responseFactory: null);

    /// <summary>
    /// Creates a handler that throws <paramref name="exception"/> instead of
    /// returning a response — e.g. an <see cref="HttpRequestException"/>,
    /// as a connection failure would.
    /// </summary>
    public static FakeHttpMessageHandler Throwing(Exception exception) => new(() => throw exception);

    /// <summary>
    /// Creates a handler that waits, without any delay or clock of its own,
    /// until the token it is handed is cancelled — by the caller, or by
    /// <see cref="HttpClient.CancelPendingRequests"/> standing in for
    /// <see cref="HttpClient.Timeout"/> — and then throws
    /// <paramref name="exception"/> instead of a cancellation exception, as a
    /// connection torn down by that cancellation would.
    /// </summary>
    public static FakeHttpMessageHandler ThrowingOnceCancelled(Exception exception) => new(async cancellationToken =>
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using (cancellationToken.Register(() => cancelled.SetResult()))
        {
            await cancelled.Task;
        }

        throw exception;
    });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_send is not null)
        {
            return _send(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _onRequest?.Invoke(request);

        if (_responseFactory is null)
        {
            throw new TaskCanceledException("Simulated HttpClient.Timeout expiry.");
        }

        return Task.FromResult(_responseFactory());
    }
}
