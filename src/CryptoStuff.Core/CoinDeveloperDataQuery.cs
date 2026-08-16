namespace CryptoStuff.Core;

/// <summary>
/// A request for a coin's developer repository activity as of a given date —
/// sourced from CoinGecko's <c>coins/{id}/history</c> endpoint despite the
/// absence of "history" from this type's name (that endpoint's own data is
/// unrelated to the "Historical market series" capability).
/// </summary>
public sealed record CoinDeveloperDataQuery
{
    /// <summary>The coin id to fetch developer activity for.</summary>
    public required string CoinId { get; init; }

    /// <summary>
    /// The date to fetch developer activity as of, raw and unvalidated —
    /// format validation is milestone `3.3`'s responsibility, not this type's.
    /// </summary>
    public required string Date { get; init; }
}
