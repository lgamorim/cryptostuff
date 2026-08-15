using System.Text.Json.Serialization;

namespace CryptoStuff.CoinGecko;

/// <summary>
/// The point-in-time snapshot returned by <c>coins/{id}/history</c>. Scoped
/// to developer activity only — coin-detail-style data (market data, image,
/// localization) is already served by <see cref="CoinGeckoCoin"/>.
/// </summary>
public sealed record CoinGeckoCoinHistory
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

    /// <summary>The coin's developer activity as of the requested date. Null when unavailable.</summary>
    [JsonPropertyName("developer_data")]
    public CoinGeckoDeveloperData? DeveloperData { get; init; }
}
