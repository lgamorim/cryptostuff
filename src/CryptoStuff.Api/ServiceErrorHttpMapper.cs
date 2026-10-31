using CryptoStuff.Core;

namespace CryptoStuff.Api;

/// <summary>Maps a <see cref="ServiceErrorCode"/> to the HTTP response the API returns for it.</summary>
internal static class ServiceErrorHttpMapper
{
    /// <summary>
    /// Returns a ProblemDetails result for <paramref name="errorCode"/>, using
    /// <paramref name="errorDetail"/> as its `detail` when one is present and
    /// the code's fixed text otherwise.
    /// </summary>
    public static IResult ToProblem(ServiceErrorCode errorCode, string? errorDetail = null)
    {
        var (statusCode, title, fixedDetail) = errorCode switch
        {
            ServiceErrorCode.NotFound => (
                StatusCodes.Status404NotFound, "Not Found", "The requested resource was not found."),
            ServiceErrorCode.RateLimited => (
                StatusCodes.Status429TooManyRequests, "Too Many Requests", "The CoinGecko rate limit was exceeded. Try again later."),
            ServiceErrorCode.RequestTimedOut => (
                StatusCodes.Status504GatewayTimeout, "Gateway Timeout", "The request to CoinGecko timed out."),
            ServiceErrorCode.UpstreamUnavailable => (
                StatusCodes.Status502BadGateway, "Bad Gateway", "CoinGecko is currently unavailable."),
            ServiceErrorCode.Conflict => (
                StatusCodes.Status409Conflict, "Conflict", "The request conflicts with the current state of the resource."),
            ServiceErrorCode.RuleViolated => (
                StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", "The request violates a domain rule."),
            ServiceErrorCode.PreconditionFailed => (
                StatusCodes.Status412PreconditionFailed, "Precondition Failed", "The resource has changed since it was last read."),
            _ => (
                StatusCodes.Status500InternalServerError, "Internal Server Error", "An unknown error occurred."),
        };

        return TypedResults.Problem(statusCode: statusCode, title: title, detail: errorDetail ?? fixedDetail);
    }
}
