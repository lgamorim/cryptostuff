using System.Text.Json.Serialization;

namespace CryptoStuff.CoinMarketCap;

/// <summary>
/// One coin's metadata, as <c>v2/cryptocurrency/info</c> returns it.
/// CoinMarketCap's documentation does not settle whether a slug lookup is
/// keyed by slug or by the coin's numeric id, so match a value by its
/// <see cref="Slug"/> or <see cref="Id"/>, never by its key. The <c>required</c>
/// members, here and in the contract types beneath it, are the ones a caller
/// reads; a body that lacks one fails to deserialize.
/// An explicit JSON <c>null</c> in place of a required member is rejected for
/// a value-type one such as <c>Id</c>, but accepted for a reference-type one,
/// which is then left null despite its declared type.
/// </summary>
public sealed record CoinMarketCapCoinInfo
{
    /// <summary>CoinMarketCap's numeric id for the coin, which <c>quotes/historical</c> takes.</summary>
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    /// <summary>The coin's display name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>The coin's ticker symbol.</summary>
    [JsonPropertyName("symbol")]
    public required string Symbol { get; init; }

    /// <summary>The coin's slug.</summary>
    [JsonPropertyName("slug")]
    public required string Slug { get; init; }

    /// <summary>The coin's description. Null when CoinMarketCap has none.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>The URL of the coin's 64-pixel logo. Null when CoinMarketCap has none.</summary>
    [JsonPropertyName("logo")]
    public string? Logo { get; init; }

    /// <summary>
    /// Every contract the coin is deployed as, one per platform it is on.
    /// Empty or absent for a coin that is no token.
    /// </summary>
    [JsonPropertyName("contract_address")]
    public IReadOnlyList<CoinMarketCapContractAddress>? ContractAddresses { get; init; }
}
