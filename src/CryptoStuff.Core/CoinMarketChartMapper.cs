using System.Globalization;
using CryptoStuff.CoinGecko;

namespace CryptoStuff.Core;

/// <summary>Maps a <c>coins/{id}/market_chart</c> chart to a <see cref="CoinMarketChartView"/>.</summary>
public static class CoinMarketChartMapper
{
    /// <summary>
    /// Maps each series in <paramref name="chart"/> point-by-point, converting
    /// each point's Unix millisecond timestamp to a <c>yyyy-MM-dd</c> date
    /// under the invariant culture, using the UTC calendar date so the result
    /// is independent of the running machine's local time zone. A series is
    /// treated as empty when explicitly JSON `null` despite its non-nullable
    /// declared type — CoinGecko's `required` only rejects the field's
    /// absence, not an explicit `null` in its place.
    /// </summary>
    public static CoinMarketChartView ToView(CoinGeckoMarketChart chart) =>
        new()
        {
            Prices = MapSeries(chart.Prices),
            MarketCaps = MapSeries(chart.MarketCaps),
            TotalVolumes = MapSeries(chart.TotalVolumes),
        };

    private static IReadOnlyList<CoinMarketChartPoint> MapSeries(IReadOnlyList<CoinGeckoMarketChartPoint>? series) =>
        (series ?? [])
            .Select(point => new CoinMarketChartPoint
            {
                Date = DateTimeOffset
                    .FromUnixTimeMilliseconds(point.UnixTimestampMilliseconds)
                    .UtcDateTime
                    .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Value = point.Value,
            })
            .ToArray();
}
