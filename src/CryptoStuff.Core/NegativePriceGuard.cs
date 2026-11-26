namespace CryptoStuff.Core;

/// <summary>
/// A decorator over <see cref="ICryptocurrencyService"/> that treats a live
/// price below zero as no price: it drops such an amount from a coin or token
/// price result, so the coin or token comes back without an amount in that
/// currency, as it does for a currency the provider does not quote it in. Zero
/// stays a price, failures pass through unchanged, and the result does not say
/// whether an amount was missing or dropped.
/// </summary>
/// <remarks>
/// Valuations and alert evaluation compute from live prices, where a negative
/// one would give a holding a negative market value or trigger an alert it
/// should not; dropping it leaves that coin unpriced while every other coin is
/// still priced. Market charts, coin detail and developer data are shown, not
/// computed from, so they pass through untouched. It sits above the selected
/// provider's service, so the rule holds whichever provider answered.
/// </remarks>
public sealed class NegativePriceGuard(ICryptocurrencyService inner) : ICryptocurrencyService
{
    /// <inheritdoc />
    public async Task<ServiceResult<CoinPriceView>> GetPricesAsync(CoinPriceQuery query, CancellationToken cancellationToken)
    {
        var result = await inner.GetPricesAsync(query, cancellationToken);
        return result is { IsSuccess: true, Value: { } view }
            ? ServiceResult<CoinPriceView>.Success(view with
            {
                Coins = [.. view.Coins.Select(coin => coin with { Amounts = WithoutNegatives(coin.Amounts) })],
            })
            : result;
    }

    /// <inheritdoc />
    public async Task<ServiceResult<TokenPriceView>> GetTokenPricesAsync(TokenPriceQuery query, CancellationToken cancellationToken)
    {
        var result = await inner.GetTokenPricesAsync(query, cancellationToken);
        return result is { IsSuccess: true, Value: { } view }
            ? ServiceResult<TokenPriceView>.Success(view with
            {
                Tokens = [.. view.Tokens.Select(token => token with { Amounts = WithoutNegatives(token.Amounts) })],
            })
            : result;
    }

    /// <inheritdoc />
    public Task<ServiceResult<CoinMarketChartView>> GetMarketChartAsync(CoinMarketChartQuery query, CancellationToken cancellationToken) =>
        inner.GetMarketChartAsync(query, cancellationToken);

    /// <inheritdoc />
    public Task<ServiceResult<CoinView>> GetCoinAsync(CoinQuery query, CancellationToken cancellationToken) =>
        inner.GetCoinAsync(query, cancellationToken);

    /// <inheritdoc />
    public Task<ServiceResult<CoinDeveloperDataView>> GetDeveloperDataAsync(CoinDeveloperDataQuery query, CancellationToken cancellationToken) =>
        inner.GetDeveloperDataAsync(query, cancellationToken);

    private static CurrencyAmount[] WithoutNegatives(IReadOnlyList<CurrencyAmount> amounts) =>
        [.. amounts.Where(amount => amount.Amount >= 0m)];
}
