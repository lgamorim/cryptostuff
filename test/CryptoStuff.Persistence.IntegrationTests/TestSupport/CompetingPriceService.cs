using CryptoStuff.Core;

namespace CryptoStuff.Persistence.IntegrationTests.TestSupport;

/// <summary>
/// A price source that runs a competing write while it answers: the caller has
/// by then read what it will write, so the competitor always commits between
/// that read and the caller's own write — a lost race as a fixed sequence of
/// steps, with no threads, timing, or delays. Only <see cref="GetPricesAsync"/>
/// is served; the other lookups report the upstream as unavailable.
/// </summary>
internal sealed class CompetingPriceService(CoinPriceView prices, Func<CancellationToken, Task> competingWrite) : ICryptocurrencyService
{
    public int GetPricesCallCount { get; private set; }

    public async Task<ServiceResult<CoinPriceView>> GetPricesAsync(CoinPriceQuery query, CancellationToken cancellationToken)
    {
        GetPricesCallCount++;
        await competingWrite(cancellationToken);
        return ServiceResult<CoinPriceView>.Success(prices);
    }

    public Task<ServiceResult<TokenPriceView>> GetTokenPricesAsync(TokenPriceQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(ServiceResult<TokenPriceView>.Failure(ServiceErrorCode.UpstreamUnavailable));

    public Task<ServiceResult<CoinMarketChartView>> GetMarketChartAsync(CoinMarketChartQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(ServiceResult<CoinMarketChartView>.Failure(ServiceErrorCode.UpstreamUnavailable));

    public Task<ServiceResult<CoinView>> GetCoinAsync(CoinQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(ServiceResult<CoinView>.Failure(ServiceErrorCode.UpstreamUnavailable));

    public Task<ServiceResult<CoinDeveloperDataView>> GetDeveloperDataAsync(CoinDeveloperDataQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(ServiceResult<CoinDeveloperDataView>.Failure(ServiceErrorCode.UpstreamUnavailable));
}
