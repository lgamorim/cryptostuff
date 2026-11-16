using CryptoStuff.Core;

namespace CryptoStuff.Persistence.IntegrationTests.TestSupport;

/// <summary>
/// A coin lookup that runs a competing write while it answers: the caller has
/// by then read what it will write, so the competitor always commits between
/// that read and the caller's own write — a lost race as a fixed sequence of
/// steps, with no threads, timing, or delays. Only <see cref="GetPricesAsync"/>
/// is served, pricing every requested coin at 1 in every requested currency;
/// the other lookups report the upstream as unavailable.
/// </summary>
internal sealed class CompetingCoinLookup(Func<CancellationToken, Task> competingWrite) : ICryptocurrencyService
{
    public Task<ServiceResult<CoinView>> GetCoinAsync(CoinQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(ServiceResult<CoinView>.Failure(ServiceErrorCode.UpstreamUnavailable));

    public async Task<ServiceResult<CoinPriceView>> GetPricesAsync(CoinPriceQuery query, CancellationToken cancellationToken)
    {
        await competingWrite(cancellationToken);
        var coins = query.CoinIds
            .Select(coinId => new CoinPrice
            {
                CoinId = coinId,
                Amounts = [.. query.VsCurrencies.Select(currency => new CurrencyAmount { Currency = currency, Amount = 1m })],
            })
            .ToList();
        return ServiceResult<CoinPriceView>.Success(new CoinPriceView { Coins = coins });
    }

    public Task<ServiceResult<TokenPriceView>> GetTokenPricesAsync(TokenPriceQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(ServiceResult<TokenPriceView>.Failure(ServiceErrorCode.UpstreamUnavailable));

    public Task<ServiceResult<CoinMarketChartView>> GetMarketChartAsync(CoinMarketChartQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(ServiceResult<CoinMarketChartView>.Failure(ServiceErrorCode.UpstreamUnavailable));

    public Task<ServiceResult<CoinDeveloperDataView>> GetDeveloperDataAsync(CoinDeveloperDataQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(ServiceResult<CoinDeveloperDataView>.Failure(ServiceErrorCode.UpstreamUnavailable));
}
