namespace CryptoStuff.Core.UnitTests.TestSupport;

/// <summary>What <see cref="ICryptocurrencyService.GetPricesAsync"/> returns, for a spy to hand back.</summary>
internal static class PriceResults
{
    /// <summary>Success with each coin priced at the given amounts, grouped by coin in first-seen order.</summary>
    public static ServiceResult<CoinPriceView> Prices(params (string CoinId, string Currency, decimal Amount)[] prices) =>
        ServiceResult<CoinPriceView>.Success(new CoinPriceView
        {
            Coins =
            [
                .. prices.GroupBy(p => p.CoinId).Select(g => new CoinPrice
                {
                    CoinId = g.Key,
                    Amounts = [.. g.Select(p => new CurrencyAmount { Currency = p.Currency, Amount = p.Amount })],
                }),
            ],
        });

    /// <summary>
    /// What upstream returns when the market data provider knows only the coins
    /// priced here: a query naming any other coin is NotFound for the whole
    /// query, as the adapter reports it, and one naming only known coins gets
    /// their prices in the currencies it asked for.
    /// </summary>
    public static Func<CoinPriceQuery, ServiceResult<CoinPriceView>> KnowsOnly(params (string CoinId, string Currency, decimal Amount)[] prices) =>
        query => query.CoinIds.All(id => prices.Any(p => p.CoinId == id))
            ? Prices([.. prices.Where(p => query.CoinIds.Contains(p.CoinId) && query.VsCurrencies.Contains(p.Currency))])
            : ServiceResult<CoinPriceView>.Failure(ServiceErrorCode.NotFound);
}
