using System.Net;

namespace CryptoStuff.CoinMarketCap;

/// <summary>
/// The outcome of a CoinMarketCap request: whether it succeeded, the HTTP
/// status code received (absent when the request timed out or got no response
/// at all), whether it timed out, the deserialized <c>data</c> on success, and
/// on failure the <c>status.error_code</c> and <c>status.error_message</c>
/// CoinMarketCap put in the body, so a caller can tell one refusal from another
/// that shares its HTTP status.
/// </summary>
public sealed record CoinMarketCapResponse<TValue>
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
    /// The deserialized <c>data</c> of the response body. Always null when
    /// <see cref="IsSuccess"/> is false; may also be null on success, when the
    /// body carries no <c>data</c> or a JSON-<c>null</c> one.
    /// </summary>
    public TValue? Value { get; }

    /// <summary>
    /// CoinMarketCap's <c>status.error_code</c> from a failure's body, such as
    /// <c>1008</c> for a minute rate limit or <c>400</c> for an invalid
    /// argument. Null on success, and on a failure whose body carries none.
    /// </summary>
    public int? ErrorCode { get; }

    /// <summary>
    /// CoinMarketCap's <c>status.error_message</c> from a failure's body. Null
    /// on success, and on a failure whose body carries none.
    /// </summary>
    public string? ErrorMessage { get; }

    private CoinMarketCapResponse(bool isSuccess, bool isTimeout, HttpStatusCode? statusCode, TValue? value, int? errorCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        IsTimeout = isTimeout;
        StatusCode = statusCode;
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>Creates a successful response carrying the deserialized value and status code.</summary>
    public static CoinMarketCapResponse<TValue> Success(TValue? value, HttpStatusCode statusCode) =>
        new(isSuccess: true, isTimeout: false, statusCode: statusCode, value: value, errorCode: null, errorMessage: null);

    /// <summary>
    /// Creates a failure response carrying the HTTP status code received and
    /// the error CoinMarketCap reported in the body, when it reported one.
    /// </summary>
    public static CoinMarketCapResponse<TValue> Failure(HttpStatusCode statusCode, int? errorCode, string? errorMessage) =>
        new(isSuccess: false, isTimeout: false, statusCode: statusCode, value: default, errorCode: errorCode, errorMessage: errorMessage);

    /// <summary>Creates a response indicating the request timed out.</summary>
    public static CoinMarketCapResponse<TValue> Timeout() =>
        new(isSuccess: false, isTimeout: true, statusCode: null, value: default, errorCode: null, errorMessage: null);

    /// <summary>
    /// Creates a failure response for a request that got no HTTP response at
    /// all — connection refused, DNS or TLS failure, connection dropped — and
    /// did not time out: no status code, not a timeout.
    /// </summary>
    public static CoinMarketCapResponse<TValue> NoResponse() =>
        new(isSuccess: false, isTimeout: false, statusCode: null, value: default, errorCode: null, errorMessage: null);
}
