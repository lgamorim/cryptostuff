using System.Text.Json.Serialization;

namespace CryptoStuff.CoinGecko;

/// <summary>
/// The three time series returned by <c>coins/{id}/market_chart</c>. Each
/// property is <c>required</c> so a response missing one of the three arrays
/// fails deserialization with a <see cref="System.Text.Json.JsonException"/>
/// instead of silently producing a null series — this does not catch an
/// explicit JSON `null` in place of an array, only the array's absence.
/// </summary>
public sealed record CoinGeckoMarketChart
{
    /// <summary>The coin's price series.</summary>
    [JsonPropertyName("prices")]
    public required IReadOnlyList<CoinGeckoMarketChartPoint> Prices { get; init; }

    /// <summary>The coin's market capitalization series.</summary>
    [JsonPropertyName("market_caps")]
    public required IReadOnlyList<CoinGeckoMarketChartPoint> MarketCaps { get; init; }

    /// <summary>The coin's trading volume series.</summary>
    [JsonPropertyName("total_volumes")]
    public required IReadOnlyList<CoinGeckoMarketChartPoint> TotalVolumes { get; init; }
}
