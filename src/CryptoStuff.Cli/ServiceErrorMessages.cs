using CryptoStuff.Core;

namespace CryptoStuff.Cli;

/// <summary>Maps a <see cref="ServiceErrorCode"/> to the message the CLI reports for it.</summary>
public static class ServiceErrorMessages
{
    /// <summary>
    /// Returns the CLI-facing message for <paramref name="errorCode"/>, or
    /// <paramref name="errorDetail"/> when one is present; a blank detail
    /// counts as none, so the message never ends at <c>Error:</c>.
    /// </summary>
    public static string For(ServiceErrorCode errorCode, string? errorDetail = null) =>
        !string.IsNullOrWhiteSpace(errorDetail) ? $"Error: {errorDetail}" : errorCode switch
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
