namespace CryptoStuff.Core;

/// <summary>The reason a Core operation failed to produce a result.</summary>
public enum ServiceErrorCode
{
    /// <summary>The requested resource does not exist upstream (CoinGecko `404`).</summary>
    NotFound,

    /// <summary>CoinGecko's rate limit was exceeded (`429`).</summary>
    RateLimited,

    /// <summary>The request to CoinGecko timed out before a response arrived.</summary>
    RequestTimedOut,

    /// <summary>CoinGecko failed for any other reason.</summary>
    UpstreamUnavailable,
}
