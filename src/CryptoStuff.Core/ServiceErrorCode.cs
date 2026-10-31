namespace CryptoStuff.Core;

/// <summary>The reason a Core operation failed to produce a result.</summary>
public enum ServiceErrorCode
{
    /// <summary>
    /// The requested resource does not exist: CoinGecko returned `404`, or no
    /// persisted record has the given id.
    /// </summary>
    NotFound,

    /// <summary>CoinGecko's rate limit was exceeded (`429`).</summary>
    RateLimited,

    /// <summary>The request to CoinGecko timed out before a response arrived.</summary>
    RequestTimedOut,

    /// <summary>CoinGecko failed for any other reason.</summary>
    UpstreamUnavailable,

    /// <summary>The write conflicts with stored state, such as a duplicate name or a lost concurrency race.</summary>
    Conflict,

    /// <summary>The write breaks a domain rule, such as an oversell or an unknown coin.</summary>
    RuleViolated,

    /// <summary>The caller's precondition on the stored version no longer holds, such as a stale `If-Match`.</summary>
    PreconditionFailed,
}
