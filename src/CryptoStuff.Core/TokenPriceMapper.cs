namespace CryptoStuff.Core;

/// <summary>Maps a <c>simple/token_price/{platform}</c> price matrix to a <see cref="TokenPriceView"/>.</summary>
public static class TokenPriceMapper
{
    /// <summary>
    /// Maps <paramref name="matrix"/> to a <see cref="TokenPriceView"/> covering
    /// every address in <paramref name="contractAddresses"/>, in that order.
    /// Assumes every address in <paramref name="contractAddresses"/> is a key
    /// of <paramref name="matrix"/> — the caller
    /// (<see cref="CryptocurrencyService"/>) is responsible for the
    /// missing-identifier check. A currency missing for an address, or an
    /// address's matrix entry that is explicitly JSON `null` despite its
    /// non-nullable declared type, is treated as that token having no
    /// amounts, not a failure.
    /// </summary>
    public static TokenPriceView ToView(
        IReadOnlyDictionary<string, Dictionary<string, decimal>> matrix,
        IReadOnlyList<string> contractAddresses,
        IReadOnlyList<string> vsCurrencies)
    {
        var tokens = contractAddresses
            .Select(address =>
            {
                var currencyAmounts = matrix[address] ?? new Dictionary<string, decimal>();
                return new TokenPrice
                {
                    ContractAddress = address,
                    Amounts = vsCurrencies
                        .Where(currencyAmounts.ContainsKey)
                        .Select(currency => new CurrencyAmount { Currency = currency, Amount = currencyAmounts[currency] })
                        .ToArray(),
                };
            })
            .ToArray();

        return new TokenPriceView { Tokens = tokens };
    }
}
