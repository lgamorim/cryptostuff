using System.Net;
using CryptoStuff.Core;

namespace CryptoStuff.CoinMarketCap;

/// <summary>Maps a failed <see cref="CoinMarketCapResponse{TValue}"/> to a <see cref="ServiceErrorCode"/>.</summary>
internal static class CoinMarketCapErrorMapper
{
    /// <summary>
    /// Maps <paramref name="response"/> to a <see cref="ServiceErrorCode"/>: a
    /// timeout becomes <see cref="ServiceErrorCode.RequestTimedOut"/>, `429`
    /// becomes <see cref="ServiceErrorCode.RateLimited"/>, and anything else —
    /// a `400` the caller has not told apart, the `401`, `402` and `403` plan
    /// refusals, any `5xx`, or no response at all — becomes
    /// <see cref="ServiceErrorCode.UpstreamUnavailable"/>. CoinMarketCap
    /// answers an unknown coin or a bad argument with `400`, never `404`, so
    /// no status maps to <see cref="ServiceErrorCode.NotFound"/> here.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="response"/> is successful.</exception>
    public static ServiceErrorCode Map<TValue>(CoinMarketCapResponse<TValue> response)
    {
        if (response.IsSuccess)
        {
            throw new ArgumentException("Cannot map a successful response to an error code.", nameof(response));
        }

        return (response.IsTimeout, response.StatusCode) switch
        {
            (true, _) => ServiceErrorCode.RequestTimedOut,
            (false, HttpStatusCode.TooManyRequests) => ServiceErrorCode.RateLimited,
            _ => ServiceErrorCode.UpstreamUnavailable,
        };
    }

    /// <summary>
    /// Whether <paramref name="response"/> is a `400` whose error message names
    /// <paramref name="parameter"/> in double quotes, as CoinMarketCap's
    /// <c>Invalid value for "convert": "XYZ"</c> does, telling a rejected
    /// argument apart from other bad requests. No test has seen a real
    /// CoinMarketCap error message yet; Milestone `8.7`'s live run confirms the
    /// format.
    /// </summary>
    public static bool RejectsParameter<TValue>(CoinMarketCapResponse<TValue> response, string parameter) =>
        response.StatusCode == HttpStatusCode.BadRequest
        && response.ErrorMessage?.Contains($"\"{parameter}\"", StringComparison.OrdinalIgnoreCase) == true;
}
