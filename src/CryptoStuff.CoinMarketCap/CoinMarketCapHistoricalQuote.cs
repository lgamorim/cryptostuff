using System.Text.Json.Serialization;

namespace CryptoStuff.CoinMarketCap;

/// <summary>A coin's quote at one point in its history, an item of <see cref="CoinMarketCapHistoricalCoin.Quotes"/>.</summary>
public sealed record CoinMarketCapHistoricalQuote
{
    /// <summary>The point in time the quote is for.</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>The quote in each requested currency, keyed by the currency's upper-case symbol (e.g. "USD").</summary>
    [JsonPropertyName("quote")]
    public required IReadOnlyDictionary<string, CoinMarketCapHistoricalCurrencyQuote> Quote { get; init; }
}
