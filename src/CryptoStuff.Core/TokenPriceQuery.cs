namespace CryptoStuff.Core;

/// <summary>A request for live prices of the given token contract addresses in the given currencies.</summary>
public sealed record TokenPriceQuery
{
    /// <summary>The platform the contract addresses belong to (e.g. "ethereum").</summary>
    public required string Platform { get; init; }

    /// <summary>The token contract addresses to price.</summary>
    public required IReadOnlyList<string> ContractAddresses { get; init; }

    /// <summary>The vs_currency codes to price each token in.</summary>
    public required IReadOnlyList<string> VsCurrencies { get; init; }
}
