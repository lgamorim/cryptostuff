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

    private FakeHttpMessageHandler(Func<HttpResponseMessage>? responseFactory)
    {
        _responseFactory = responseFactory;
    }

    /// <summary>Creates a handler that returns the given status code and JSON body.</summary>
    public static FakeHttpMessageHandler ReturningJson(HttpStatusCode statusCode, string json) =>
        new(() => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

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

        if (_responseFactory is null)
        {
            throw new TaskCanceledException("Simulated HttpClient.Timeout expiry.");
        }

        return Task.FromResult(_responseFactory());
    }
}
