namespace CryptoStuff.Core;

/// <summary>
/// The amounts of one price lookup, indexed by coin and currency, so pricing
/// many holdings or alerts against a single answer is one hash lookup each
/// rather than a scan of every coin in it.
/// </summary>
internal sealed class PriceTable
{
    private readonly Dictionary<(string CoinId, string Currency), decimal> _amounts = [];

    public PriceTable(IEnumerable<CoinPrice> coins)
    {
        foreach (var coin in coins)
        {
            foreach (var amount in coin.Amounts)
            {
                // The first quote wins, should a lookup ever repeat one.
                _amounts.TryAdd((coin.CoinId, amount.Currency), amount.Amount);
            }
        }
    }

    /// <summary>
    /// Gets <paramref name="coinId"/>'s amount in <paramref name="currency"/>,
    /// returning false when the lookup gave none: the coin was not known
    /// upstream, or not quoted in that currency.
    /// </summary>
    public bool TryGetAmount(string coinId, string currency, out decimal amount) =>
        _amounts.TryGetValue((coinId, currency), out amount);
}
