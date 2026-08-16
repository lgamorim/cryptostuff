namespace CryptoStuff.Core;

/// <summary>The live prices for the requested coins.</summary>
public sealed record CoinPriceView
{
    /// <summary>One entry per requested coin, in the order requested.</summary>
    public required IReadOnlyList<CoinPrice> Coins { get; init; }
}

/// <summary>A single coin's prices across the requested currencies.</summary>
public sealed record CoinPrice
{
    /// <summary>The coin's id.</summary>
    public required string CoinId { get; init; }

    /// <summary>The coin's price in each requested currency present in the upstream response.</summary>
    public required IReadOnlyList<CurrencyAmount> Amounts { get; init; }
}
