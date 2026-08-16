namespace CryptoStuff.Core;

/// <summary>A request for live prices of the given coins in the given currencies.</summary>
public sealed record CoinPriceQuery
{
    /// <summary>The coin ids to price.</summary>
    public required IReadOnlyList<string> CoinIds { get; init; }

    /// <summary>The vs_currency codes to price each coin in.</summary>
    public required IReadOnlyList<string> VsCurrencies { get; init; }
}
