using System.Net;

namespace CryptoStuff.Composition;

/// <summary>
/// Retries a request when CoinGecko responds <c>429 Too Many Requests</c>, up
/// to a bounded number of times. Honours the <c>Retry-After</c> response
/// header in both its delta-seconds and HTTP-date forms; falls back to a
/// constant backoff when the header is absent. Any other status code —
/// success or failure — passes straight through untouched. GET-only: a
/// retried request's <see cref="HttpRequestMessage.Content"/> is not
/// preserved, matching <c>ICoinGeckoClient</c>'s GET-only contract — sending
/// a request with content through this handler silently drops that content
/// on any retry.
/// </summary>
public sealed class RateLimitRetryHandler : DelegatingHandler
{
    private readonly int _maxRetryAttempts;
    private readonly TimeSpan _constantBackoff;
    private readonly TimeProvider _timeProvider;

    /// <param name="maxRetryAttempts">The maximum number of retries. Total sends on full exhaustion are <c>1 + maxRetryAttempts</c>.</param>
    /// <param name="constantBackoff">The delay used when a <c>429</c> response carries no <c>Retry-After</c> header.</param>
    /// <param name="timeProvider">The clock used for delays. Defaults to <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxRetryAttempts"/> is negative, or
    /// <paramref name="constantBackoff"/> is negative.
    /// </exception>
    public RateLimitRetryHandler(int maxRetryAttempts, TimeSpan constantBackoff, TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetryAttempts);
        ArgumentOutOfRangeException.ThrowIfLessThan(constantBackoff, TimeSpan.Zero);

        _maxRetryAttempts = maxRetryAttempts;
        _constantBackoff = constantBackoff;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var requestToSend = attempt == 0 ? request : CloneRequest(request);
            var response = await base.SendAsync(requestToSend, cancellationToken);
            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt >= _maxRetryAttempts)
            {
                return response;
            }

            // Read the Retry-After header before disposing the response.
            var delay = GetRetryDelay(response);
            response.Dispose();
            await Task.Delay(delay, _timeProvider, cancellationToken);
        }
    }

    private TimeSpan GetRetryDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var remaining = date - _timeProvider.GetUtcNow();
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        return _constantBackoff;
    }

    /// <summary>
    /// Clones <paramref name="request"/> so it can be sent again — an
    /// <see cref="HttpRequestMessage"/> cannot be resent once sent. Content
    /// is not cloned since <c>ICoinGeckoClient</c> is GET-only by its own
    /// documented contract.
    /// </summary>
    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
