using System.Text.Json.Serialization;

namespace CryptoStuff.CoinMarketCap;

/// <summary>One contract a coin is deployed as, an item of <see cref="CoinMarketCapCoinInfo.ContractAddresses"/>.</summary>
public sealed record CoinMarketCapContractAddress
{
    /// <summary>The contract's address on its platform.</summary>
    [JsonPropertyName("contract_address")]
    public required string Address { get; init; }

    /// <summary>The platform the contract is deployed on.</summary>
    [JsonPropertyName("platform")]
    public required CoinMarketCapContractPlatform Platform { get; init; }
}
