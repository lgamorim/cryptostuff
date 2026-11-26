namespace CryptoStuff.Core;

/// <summary>The reason a Core operation failed to produce a result.</summary>
public enum ServiceErrorCode
{
    /// <summary>
    /// The requested resource does not exist: the market data provider returned `404`, or no
    /// persisted record has the given id.
    /// </summary>
    NotFound,

    /// <summary>The market data provider's rate limit was exceeded (`429`).</summary>
    RateLimited,

    /// <summary>The request to the market data provider timed out before a response arrived.</summary>
    RequestTimedOut,

    /// <summary>The market data provider failed for any other reason.</summary>
    UpstreamUnavailable,

    /// <summary>The write conflicts with stored state, such as a duplicate name or a lost concurrency race.</summary>
    Conflict,

    /// <summary>
    /// The request breaks a domain rule, such as a write that would oversell
    /// or name an unknown coin, or a valuation with an amount too large to compute.
    /// </summary>
    RuleViolated,

    /// <summary>The caller's precondition on the stored version no longer holds, such as a stale `If-Match`.</summary>
    PreconditionFailed,

    /// <summary>
    /// The selected market data provider cannot serve the operation at all, such
    /// as developer data from a provider that has none; the detail names the provider.
    /// </summary>
    Unsupported,
}
