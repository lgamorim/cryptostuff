namespace CryptoStuff.Core;

/// <summary>The live prices for the requested token contract addresses.</summary>
public sealed record TokenPriceView
{
    /// <summary>One entry per requested contract address, in the order requested.</summary>
    public required IReadOnlyList<TokenPrice> Tokens { get; init; }
}

/// <summary>A single token's prices across the requested currencies.</summary>
public sealed record TokenPrice
{
    /// <summary>The token's contract address.</summary>
    public required string ContractAddress { get; init; }

    /// <summary>The token's price in each requested currency present in the upstream response.</summary>
    public required IReadOnlyList<CurrencyAmount> Amounts { get; init; }
}
