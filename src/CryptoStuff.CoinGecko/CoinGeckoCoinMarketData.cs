using System.Text.Json.Serialization;
using CoinGeckoCurrencySnapshot = System.Collections.Generic.IReadOnlyDictionary<string, decimal>;

namespace CryptoStuff.CoinGecko;

/// <summary>
/// A coin's market data: per-currency price, market cap, and trading volume,
/// each keyed by a lowercase vs_currency code. Individual currency values
/// are assumed to never be an explicit JSON `null` when present — if that
/// assumption is wrong, deserialization fails with a
/// <see cref="System.Text.Json.JsonException"/> for the whole response,
/// rather than silently omitting that one currency. Separately, the
/// `required` members here only reject a field's absence, not an explicit
/// JSON `null` in its place.
/// </summary>
public sealed record CoinGeckoCoinMarketData
{
    /// <summary>Current price per currency.</summary>
    [JsonPropertyName("current_price")]
    public required CoinGeckoCurrencySnapshot CurrentPrice { get; init; }

    /// <summary>Market capitalization per currency.</summary>
    [JsonPropertyName("market_cap")]
    public required CoinGeckoCurrencySnapshot MarketCap { get; init; }

    /// <summary>Trading volume per currency.</summary>
    [JsonPropertyName("total_volume")]
    public required CoinGeckoCurrencySnapshot TotalVolume { get; init; }
}
