namespace CryptoStuff.Core;

/// <summary>
/// The checks the feature services make with the market data provider before they store a
/// coin id: one price lookup for the coin, which reports a coin the market data provider
/// does not know as <see cref="ServiceErrorCode.NotFound"/>. Each returns the
/// failure to report, or null when the coin passes: an unknown coin, or one
/// with no price where a price is required, is a
/// <see cref="ServiceErrorCode.RuleViolated"/> naming it; any other upstream
/// failure is passed on unchanged.
/// </summary>
internal static class UpstreamCoinChecks
{
    // The currency a coin is looked up in when only its existence matters.
    // The market data provider answers for a coin it knows even when it does not quote it in
    // the currency asked for, so the choice only has to be one it accepts.
    private const string ExistenceCurrency = "usd";

    /// <summary>Checks that the market data provider knows <paramref name="coinId"/>.</summary>
    public static Task<ServiceResult<TValue>?> CheckKnownAsync<TValue>(
        ICryptocurrencyService cryptocurrencyService, string coinId, CancellationToken cancellationToken) =>
        CheckAsync<TValue>(cryptocurrencyService, coinId, ExistenceCurrency, requirePrice: false, cancellationToken);

    /// <summary>
    /// Checks that the market data provider knows <paramref name="coinId"/> and prices it in
    /// <paramref name="vsCurrency"/>, the lookup a later valuation or alert
    /// evaluation will make.
    /// </summary>
    public static Task<ServiceResult<TValue>?> CheckPricedAsync<TValue>(
        ICryptocurrencyService cryptocurrencyService, string coinId, string vsCurrency, CancellationToken cancellationToken) =>
        CheckAsync<TValue>(cryptocurrencyService, coinId, vsCurrency, requirePrice: true, cancellationToken);

    private static async Task<ServiceResult<TValue>?> CheckAsync<TValue>(
        ICryptocurrencyService cryptocurrencyService,
        string coinId,
        string vsCurrency,
        bool requirePrice,
        CancellationToken cancellationToken)
    {
        var priceQuery = new CoinPriceQuery { CoinIds = [coinId], VsCurrencies = [vsCurrency] };
        var prices = await cryptocurrencyService.GetPricesAsync(priceQuery, cancellationToken);
        if (prices.ErrorCode is { } errorCode)
        {
            return errorCode == ServiceErrorCode.NotFound
                ? ServiceResult<TValue>.Failure(ServiceErrorCode.RuleViolated, $"The market data provider has no coin with id '{coinId}'.")
                : ServiceResult<TValue>.Failure(errorCode, prices.ErrorDetail);
        }

        if (!requirePrice)
        {
            return null;
        }

        // A currency the market data provider doesn't know is not an error upstream: the
        // coin just comes back without an amount in it.
        // `!` is safe: a successful result always carries its value.
        var pricedInCurrency = prices.Value!.Coins.Any(c => c.Amounts.Any(a => a.Currency == vsCurrency));
        return pricedInCurrency
            ? null
            : ServiceResult<TValue>.Failure(ServiceErrorCode.RuleViolated, $"The market data provider has no price for '{coinId}' in '{vsCurrency}'.");
    }
}
