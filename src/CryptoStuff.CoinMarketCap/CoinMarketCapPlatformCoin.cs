using System.Text.Json.Serialization;

namespace CryptoStuff.CoinMarketCap;

/// <summary>A platform's own coin, as a <see cref="CoinMarketCapContractPlatform"/> names it.</summary>
public sealed record CoinMarketCapPlatformCoin
{
    /// <summary>The coin's slug (e.g. "ethereum"), which identifies the platform.</summary>
    [JsonPropertyName("slug")]
    public required string Slug { get; init; }
}
