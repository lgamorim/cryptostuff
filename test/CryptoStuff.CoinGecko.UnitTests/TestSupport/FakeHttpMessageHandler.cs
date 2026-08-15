using System.Net;
using System.Text;

namespace CryptoStuff.CoinGecko.UnitTests.TestSupport;

/// <summary>
/// A stand-in <see cref="HttpMessageHandler"/> that never performs real
/// network I/O. Reused by every CoinGecko milestone's tests.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpResponseMessage>? _responseFactory;
    private readonly Action<HttpRequestMessage>? _onRequest;

    private FakeHttpMessageHandler(Func<HttpResponseMessage>? responseFactory, Action<HttpRequestMessage>? onRequest = null)
    {
        _responseFactory = responseFactory;
        _onRequest = onRequest;
    }

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

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _onRequest?.Invoke(request);

        if (_responseFactory is null)
        {
            throw new TaskCanceledException("Simulated HttpClient.Timeout expiry.");
        }

        return Task.FromResult(_responseFactory());
    }
}
