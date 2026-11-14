namespace CryptoStuff.Core;

/// <summary>
/// Price lookups for coins the app already holds, watches or alerts on, where
/// CoinGecko delisting one of them must not hide the prices of the rest.
/// </summary>
public static class KnownCoinPrices
{
    /// <summary>
    /// Gets the prices <paramref name="query"/> asks for, as
    /// <see cref="ICryptocurrencyService.GetPricesAsync"/> does, except that a
    /// coin CoinGecko does not know comes back with no amounts, as a coin not
    /// quoted in a requested currency already does, instead of failing the
    /// whole lookup. Every requested coin is in the result, in query order.
    /// </summary>
    /// <remarks>
    /// One upstream call covers the whole query. CoinGecko omits a coin it no
    /// longer knows, which the adapter reports as
    /// <see cref="ServiceErrorCode.NotFound"/> for the whole call without naming
    /// the coin; only then is each coin asked for on its own, in every
    /// requested currency, to tell the known coins from the unknown ones. With
    /// a single coin the one call already was that coin's own, so it is not
    /// repeated. The adapter also reports an HTTP <c>404</c> from the price
    /// endpoint itself as <see cref="ServiceErrorCode.NotFound"/>, which cannot
    /// be told apart, so that too leaves every coin without amounts. Any other
    /// failure, from either kind of call, is passed on unchanged.
    /// </remarks>
    public static async Task<ServiceResult<CoinPriceView>> GetKnownCoinPricesAsync(
        this ICryptocurrencyService cryptocurrencyService, CoinPriceQuery query, CancellationToken cancellationToken)
    {
        var all = await cryptocurrencyService.GetPricesAsync(query, cancellationToken);
        if (all.ErrorCode != ServiceErrorCode.NotFound)
        {
            return all;
        }

        if (query.CoinIds.Count == 1)
        {
            return ServiceResult<CoinPriceView>.Success(new CoinPriceView { Coins = [NoAmounts(query.CoinIds[0])] });
        }

        var coins = new List<CoinPrice>();
        foreach (var coinId in query.CoinIds)
        {
            var one = await cryptocurrencyService.GetPricesAsync(query with { CoinIds = [coinId] }, cancellationToken);
            if (one.ErrorCode == ServiceErrorCode.NotFound)
            {
                coins.Add(NoAmounts(coinId));
                continue;
            }

            if (one.ErrorCode is { } errorCode)
            {
                return ServiceResult<CoinPriceView>.Failure(errorCode, one.ErrorDetail);
            }

            // `!` is safe: a successful result always carries its value.
            coins.AddRange(one.Value!.Coins);
        }

        return ServiceResult<CoinPriceView>.Success(new CoinPriceView { Coins = coins });
    }

    private static CoinPrice NoAmounts(string coinId) => new() { CoinId = coinId, Amounts = [] };
}
