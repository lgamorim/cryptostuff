namespace CryptoStuff.Core;

/// <summary>A request for a coin's detail.</summary>
public sealed record CoinQuery
{
    /// <summary>The coin id to fetch.</summary>
    public required string CoinId { get; init; }
}
