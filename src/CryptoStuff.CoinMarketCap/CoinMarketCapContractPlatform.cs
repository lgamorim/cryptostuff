using System.Text.Json.Serialization;

namespace CryptoStuff.CoinMarketCap;

/// <summary>The platform a <see cref="CoinMarketCapContractAddress"/> is deployed on.</summary>
public sealed record CoinMarketCapContractPlatform
{
    /// <summary>The platform's own coin, whose slug identifies the platform.</summary>
    [JsonPropertyName("coin")]
    public required CoinMarketCapPlatformCoin Coin { get; init; }
}
