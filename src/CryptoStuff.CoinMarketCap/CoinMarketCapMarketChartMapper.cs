using System.Globalization;
using CryptoStuff.Core;

namespace CryptoStuff.CoinMarketCap;

/// <summary>Maps a coin's <c>quotes/historical</c> series to the app's <see cref="CoinMarketChartView"/>.</summary>
internal static class CoinMarketCapMarketChartMapper
{
    /// <summary>
    /// Maps each of <paramref name="coin"/>'s points by its quote in
    /// <paramref name="currency"/>, CoinMarketCap's upper-case symbol, dated
    /// <c>yyyy-MM-dd</c> by its UTC day, oldest first as CoinMarketCap lists
    /// them. A series leaves out a point whose value in it is missing.
    /// </summary>
    /// <remarks>
    /// Upstream JSON's explicit nulls, which <c>required</c> lets through, are
    /// skipped: a null quote list gives three empty series, and a null point,
    /// quote map or quote leaves that point out of all three.
    /// </remarks>
    public static CoinMarketChartView Map(CoinMarketCapHistoricalCoin coin, string currency)
    {
        var dated = new List<(string Date, CoinMarketCapHistoricalCurrencyQuote Quote)>();
        foreach (var point in coin.Quotes ?? [])
        {
            if (point?.Quote?.GetValueOrDefault(currency) is { } quote)
            {
                dated.Add((point.Timestamp.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), quote));
            }
        }

        return new CoinMarketChartView
        {
            Prices = SeriesOf(dated, quote => quote.Price),
            MarketCaps = SeriesOf(dated, quote => quote.MarketCap),
            TotalVolumes = SeriesOf(dated, quote => quote.Volume24h),
        };
    }

    private static CoinMarketChartPoint[] SeriesOf(
        IEnumerable<(string Date, CoinMarketCapHistoricalCurrencyQuote Quote)> dated,
        Func<CoinMarketCapHistoricalCurrencyQuote, decimal?> valueOf) =>
        dated
            .Select(entry => (entry.Date, Value: valueOf(entry.Quote)))
            .Where(point => point.Value is not null)
            .Select(point => new CoinMarketChartPoint { Date = point.Date, Value = point.Value.GetValueOrDefault() })
            .ToArray();
}
