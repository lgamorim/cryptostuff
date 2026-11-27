using System.Text.Json.Serialization;

namespace CryptoStuff.CoinMarketCap;

/// <summary>
/// A coin's latest market data in one currency, an item of a
/// <see cref="CoinMarketCapLatestQuote.Quote"/> list. The figures are null
/// when CoinMarketCap has none for the coin, as for one no longer traded.
/// </summary>
public sealed record CoinMarketCapCurrencyQuote
{
    /// <summary>The currency's symbol, upper case (e.g. "USD").</summary>
    [JsonPropertyName("symbol")]
    public required string Symbol { get; init; }

    /// <summary>The coin's price in the currency.</summary>
    [JsonPropertyName("price")]
    public decimal? Price { get; init; }

    /// <summary>The coin's trading volume over the last 24 hours, in the currency.</summary>
    [JsonPropertyName("volume_24h")]
    public decimal? Volume24h { get; init; }

    /// <summary>The coin's market capitalization in the currency.</summary>
    [JsonPropertyName("market_cap")]
    public decimal? MarketCap { get; init; }

    /// <summary>When CoinMarketCap last updated the quote.</summary>
    [JsonPropertyName("last_updated")]
    public DateTimeOffset? LastUpdated { get; init; }
}
