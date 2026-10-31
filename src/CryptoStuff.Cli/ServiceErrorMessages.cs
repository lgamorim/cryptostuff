using CryptoStuff.Core;

namespace CryptoStuff.Cli;

/// <summary>Maps a <see cref="ServiceErrorCode"/> to the message the CLI reports for it.</summary>
internal static class ServiceErrorMessages
{
    /// <summary>
    /// Returns the CLI-facing message for <paramref name="errorCode"/>, or
    /// <paramref name="errorDetail"/> when one is present.
    /// </summary>
    public static string For(ServiceErrorCode errorCode, string? errorDetail = null) =>
        errorDetail is not null ? $"Error: {errorDetail}" : errorCode switch
        {
            ServiceErrorCode.NotFound => "Error: the requested resource was not found.",
            ServiceErrorCode.RateLimited => "Error: the CoinGecko rate limit was exceeded. Try again later.",
            ServiceErrorCode.RequestTimedOut => "Error: the request to CoinGecko timed out.",
            ServiceErrorCode.UpstreamUnavailable => "Error: CoinGecko is currently unavailable.",
            ServiceErrorCode.Conflict => "Error: the request conflicts with existing data.",
            ServiceErrorCode.RuleViolated => "Error: the request violates a domain rule.",
            ServiceErrorCode.PreconditionFailed => "Error: the data has changed since it was last read.",
            _ => "Error: an unknown error occurred.",
        };
}
