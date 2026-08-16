using CryptoStuff.CoinGecko;

namespace CryptoStuff.Core;

/// <summary>Maps a <c>coins/{id}</c> coin to a <see cref="CoinView"/>.</summary>
public static class CoinMapper
{
    /// <summary>
    /// Maps <paramref name="coin"/> to a <see cref="CoinView"/>: the English
    /// description is selected from the localization dictionary, the image
    /// URL falls back from large to small to thumbnail, and market data — if
    /// present — is joined per currency present in all of current price,
    /// market cap, and volume, ordered by currency code. CoinGecko's
    /// `required` members only reject a field's absence, not an explicit
    /// JSON `null` in its place, so <see cref="CoinGeckoCoin.Image"/> and
    /// <see cref="CoinGeckoCoinMarketData"/>'s three dictionaries are treated
    /// as possibly null despite their non-nullable declared types.
    /// </summary>
    public static CoinView ToView(CoinGeckoCoin coin) =>
        new()
        {
            Id = coin.Id,
            Symbol = coin.Symbol,
            Name = coin.Name,
            Description = coin.Description?.GetValueOrDefault("en"),
            ImageUrl = coin.Image?.Large ?? coin.Image?.Small ?? coin.Image?.Thumb,
            MarketData = coin.MarketData is null ? null : MapMarketData(coin.MarketData),
        };

    private static IReadOnlyList<CoinCurrencySnapshot> MapMarketData(CoinGeckoCoinMarketData marketData)
    {
        var currentPrice = marketData.CurrentPrice ?? new Dictionary<string, decimal>();
        var marketCap = marketData.MarketCap ?? new Dictionary<string, decimal>();
        var totalVolume = marketData.TotalVolume ?? new Dictionary<string, decimal>();

        var currencies = currentPrice.Keys
            .Intersect(marketCap.Keys)
            .Intersect(totalVolume.Keys)
            .OrderBy(currency => currency, StringComparer.Ordinal);

        return currencies
            .Select(currency => new CoinCurrencySnapshot
            {
                Currency = currency,
                Price = currentPrice[currency],
                MarketCap = marketCap[currency],
                Volume = totalVolume[currency],
            })
            .ToArray();
    }
}
