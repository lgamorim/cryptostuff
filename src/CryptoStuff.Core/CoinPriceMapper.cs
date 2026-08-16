namespace CryptoStuff.Core;

/// <summary>Maps a <c>simple/price</c> price matrix to a <see cref="CoinPriceView"/>.</summary>
public static class CoinPriceMapper
{
    /// <summary>
    /// Maps <paramref name="matrix"/> to a <see cref="CoinPriceView"/> covering
    /// every id in <paramref name="coinIds"/>, in that order. Assumes every id
    /// in <paramref name="coinIds"/> is a key of <paramref name="matrix"/> —
    /// the caller (<see cref="CryptocurrencyService"/>) is responsible for the
    /// missing-identifier check. A currency missing for a coin, or a coin's
    /// matrix entry that is explicitly JSON `null` despite its non-nullable
    /// declared type, is treated as that coin having no amounts, not a
    /// failure.
    /// </summary>
    public static CoinPriceView ToView(
        IReadOnlyDictionary<string, Dictionary<string, decimal>> matrix,
        IReadOnlyList<string> coinIds,
        IReadOnlyList<string> vsCurrencies)
    {
        var coins = coinIds
            .Select(id =>
            {
                var currencyAmounts = matrix[id] ?? new Dictionary<string, decimal>();
                return new CoinPrice
                {
                    CoinId = id,
                    Amounts = vsCurrencies
                        .Where(currencyAmounts.ContainsKey)
                        .Select(currency => new CurrencyAmount { Currency = currency, Amount = currencyAmounts[currency] })
                        .ToArray(),
                };
            })
            .ToArray();

        return new CoinPriceView { Coins = coins };
    }
}
