using System.Net;

namespace CryptoStuff.CoinGecko;

/// <summary>
/// The outcome of a CoinGecko request: whether it succeeded, the HTTP status
/// code received (absent when the request timed out or got no response at
/// all), whether it timed out, and the deserialized value on success.
/// </summary>
public sealed record CoinGeckoResponse<TValue>
{
    /// <summary>Whether the request completed successfully.</summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Whether the request timed out, as distinct from the caller cancelling
    /// its own <see cref="CancellationToken"/> — a cancellation propagates as
    /// a thrown exception instead and is never represented here.
    /// </summary>
    public bool IsTimeout { get; }

    /// <summary>
    /// The HTTP status code received. Null only when no response arrived: when
    /// <see cref="IsTimeout"/> is true, or when the request got no response at
    /// all (see <see cref="NoResponse"/>).
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// The deserialized response body. Always null when <see cref="IsSuccess"/>
    /// is false; may also be null on success, since a JSON-`null` body
    /// deserializes to null without that being a failure. A truly empty body
    /// is not covered by this — it fails JSON deserialization instead.
    /// </summary>
    public TValue? Value { get; }

    private CoinGeckoResponse(bool isSuccess, bool isTimeout, HttpStatusCode? statusCode, TValue? value)
    {
        IsSuccess = isSuccess;
        IsTimeout = isTimeout;
        StatusCode = statusCode;
        Value = value;
    }

    /// <summary>Creates a successful response carrying the deserialized value and status code.</summary>
    public static CoinGeckoResponse<TValue> Success(TValue? value, HttpStatusCode statusCode) =>
        new(isSuccess: true, isTimeout: false, statusCode: statusCode, value: value);

    /// <summary>Creates a failure response carrying the HTTP status code received.</summary>
    public static CoinGeckoResponse<TValue> Failure(HttpStatusCode statusCode) =>
        new(isSuccess: false, isTimeout: false, statusCode: statusCode, value: default);

    /// <summary>Creates a response indicating the request timed out.</summary>
    public static CoinGeckoResponse<TValue> Timeout() =>
        new(isSuccess: false, isTimeout: true, statusCode: null, value: default);

    /// <summary>
    /// Creates a failure response for a request that got no HTTP response at
    /// all — connection refused, DNS or TLS failure, connection dropped — and
    /// did not time out: no status code, not a timeout.
    /// </summary>
    public static CoinGeckoResponse<TValue> NoResponse() =>
        new(isSuccess: false, isTimeout: false, statusCode: null, value: default);
}
