using CryptoStuff.Core;

namespace CryptoStuff.Cli;

/// <summary>Maps a <see cref="ServiceErrorCode"/> to the message the CLI reports for it.</summary>
internal static class ServiceErrorMessages
{
    /// <summary>Returns the CLI-facing message for <paramref name="errorCode"/>.</summary>
    public static string For(ServiceErrorCode errorCode) => errorCode switch
    {
        ServiceErrorCode.NotFound => "Error: the requested resource was not found.",
        ServiceErrorCode.RateLimited => "Error: the CoinGecko rate limit was exceeded. Try again later.",
        ServiceErrorCode.RequestTimedOut => "Error: the request to CoinGecko timed out.",
        ServiceErrorCode.UpstreamUnavailable => "Error: CoinGecko is currently unavailable.",
        _ => "Error: an unknown error occurred.",
    };
}
