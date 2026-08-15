using System.Text.Json.Serialization;

namespace CryptoStuff.CoinGecko;

/// <summary>
/// The coin detail returned by <c>coins/{id}</c>. The <c>required</c>
/// members only reject the field's absence, not an explicit JSON `null` in
/// its place — a response with e.g. `"image": null` still deserializes,
/// leaving that member null despite its declared type.
/// </summary>
public sealed record CoinGeckoCoin
{
    /// <summary>The coin's id.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>The coin's ticker symbol.</summary>
    [JsonPropertyName("symbol")]
    public required string Symbol { get; init; }

    /// <summary>The coin's display name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>The coin's description, keyed by language code (e.g. "en").</summary>
    [JsonPropertyName("description")]
    public IReadOnlyDictionary<string, string>? Description { get; init; }

    /// <summary>The coin's image at three sizes.</summary>
    [JsonPropertyName("image")]
    public required CoinGeckoImage Image { get; init; }

    /// <summary>The coin's market data. Null when the coin has no trading data.</summary>
    [JsonPropertyName("market_data")]
    public CoinGeckoCoinMarketData? MarketData { get; init; }
}
