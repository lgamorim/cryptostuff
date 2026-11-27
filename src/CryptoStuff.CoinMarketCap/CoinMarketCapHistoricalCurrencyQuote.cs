using System.Text.Json.Serialization;

namespace CryptoStuff.CoinMarketCap;

/// <summary>
/// A coin's market data in one currency at one point in its history, a value
/// of <see cref="CoinMarketCapHistoricalQuote.Quote"/>. The figures are null
/// when CoinMarketCap has none for that point.
/// </summary>
public sealed record CoinMarketCapHistoricalCurrencyQuote
{
    /// <summary>The coin's price in the currency.</summary>
    [JsonPropertyName("price")]
    public decimal? Price { get; init; }

    /// <summary>The coin's trading volume over the 24 hours to this point, in the currency.</summary>
    [JsonPropertyName("volume_24h")]
    public decimal? Volume24h { get; init; }

    /// <summary>The coin's market capitalization in the currency.</summary>
    [JsonPropertyName("market_cap")]
    public decimal? MarketCap { get; init; }
}
