using System.Text.Json.Serialization;

namespace CryptoStuff.CoinGecko;

/// <summary>
/// One [timestamp, value] row from a CoinGecko market chart series.
/// </summary>
/// <param name="UnixTimestampMilliseconds">
/// The row's Unix timestamp in milliseconds, preserved raw. Left unconverted
/// here since Core's mapping layer, not this client, owns turning it into a
/// display date.
/// </param>
/// <param name="Value">The row's value (price, market cap, or volume, depending on the series).</param>
[JsonConverter(typeof(CoinGeckoMarketChartPointConverter))]
public sealed record CoinGeckoMarketChartPoint(long UnixTimestampMilliseconds, decimal Value);
