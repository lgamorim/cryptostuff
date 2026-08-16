using System.Net;
using CryptoStuff.CoinGecko;

namespace CryptoStuff.Core;

/// <summary>Maps a failed <see cref="CoinGeckoResponse{TValue}"/> to a <see cref="ServiceErrorCode"/>.</summary>
public static class ServiceErrorCodeMapper
{
    /// <summary>
    /// Maps <paramref name="response"/>'s timeout flag and HTTP status code to
    /// a <see cref="ServiceErrorCode"/>: a timeout becomes
    /// <see cref="ServiceErrorCode.RequestTimedOut"/>, `404` becomes
    /// <see cref="ServiceErrorCode.NotFound"/>, `429` becomes
    /// <see cref="ServiceErrorCode.RateLimited"/>, and anything else becomes
    /// <see cref="ServiceErrorCode.UpstreamUnavailable"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="response"/> is successful.</exception>
    public static ServiceErrorCode Map<TValue>(CoinGeckoResponse<TValue> response)
    {
        if (response.IsSuccess)
        {
            throw new ArgumentException("Cannot map a successful response to an error code.", nameof(response));
        }

        return (response.IsTimeout, response.StatusCode) switch
        {
            (true, _) => ServiceErrorCode.RequestTimedOut,
            (false, HttpStatusCode.NotFound) => ServiceErrorCode.NotFound,
            (false, HttpStatusCode.TooManyRequests) => ServiceErrorCode.RateLimited,
            _ => ServiceErrorCode.UpstreamUnavailable,
        };
    }
}
