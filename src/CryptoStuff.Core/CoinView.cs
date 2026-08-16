namespace CryptoStuff.Core;

/// <summary>A coin's detail.</summary>
public sealed record CoinView
{
    /// <summary>The coin's id.</summary>
    public required string Id { get; init; }

    /// <summary>The coin's ticker symbol.</summary>
    public required string Symbol { get; init; }

    /// <summary>The coin's display name.</summary>
    public required string Name { get; init; }

    /// <summary>The coin's English description. Null when unavailable.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// The coin's image URL, preferring the large size, falling back to
    /// small, then thumbnail. Null when no size is available.
    /// </summary>
    public string? ImageUrl { get; init; }

    /// <summary>
    /// The coin's current price, market cap, and volume, per currency present
    /// in all three. Null when the coin has no trading data.
    /// </summary>
    public IReadOnlyList<CoinCurrencySnapshot>? MarketData { get; init; }
}

/// <summary>A coin's price, market cap, and volume snapshot in a single currency.</summary>
public sealed record CoinCurrencySnapshot
{
    /// <summary>The lowercase vs_currency code (e.g. "usd").</summary>
    public required string Currency { get; init; }

    /// <summary>The coin's current price in <see cref="Currency"/>.</summary>
    public required decimal Price { get; init; }

    /// <summary>The coin's market capitalization in <see cref="Currency"/>.</summary>
    public required decimal MarketCap { get; init; }

    /// <summary>The coin's trading volume in <see cref="Currency"/>.</summary>
    public required decimal Volume { get; init; }
}
