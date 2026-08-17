using CryptoStuff.Core;

namespace CryptoStuff.Api;

/// <summary>Maps a <see cref="ServiceErrorCode"/> to the HTTP response the API returns for it.</summary>
internal static class ServiceErrorHttpMapper
{
    /// <summary>Returns a ProblemDetails result for <paramref name="errorCode"/>.</summary>
    public static IResult ToProblem(ServiceErrorCode errorCode) => errorCode switch
    {
        ServiceErrorCode.NotFound => TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            detail: "The requested resource was not found."),
        ServiceErrorCode.RateLimited => TypedResults.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Too Many Requests",
            detail: "The CoinGecko rate limit was exceeded. Try again later."),
        ServiceErrorCode.RequestTimedOut => TypedResults.Problem(
            statusCode: StatusCodes.Status504GatewayTimeout,
            title: "Gateway Timeout",
            detail: "The request to CoinGecko timed out."),
        ServiceErrorCode.UpstreamUnavailable => TypedResults.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "Bad Gateway",
            detail: "CoinGecko is currently unavailable."),
        _ => TypedResults.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An unknown error occurred."),
    };
}
