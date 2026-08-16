namespace CryptoStuff.Core.UnitTests.TestSupport;

internal sealed class SpyCryptocurrencyService : ICryptocurrencyService
{
    public int GetPricesCallCount { get; private set; }

    public int GetTokenPricesCallCount { get; private set; }

    public int GetMarketChartCallCount { get; private set; }

    public int GetCoinCallCount { get; private set; }

    public int GetDeveloperDataCallCount { get; private set; }

    public CancellationToken CapturedCancellationToken { get; private set; }

    public Exception? ExceptionToThrow { get; set; }

    public ServiceResult<CoinPriceView> PricesResult { get; set; } =
        ServiceResult<CoinPriceView>.Success(new CoinPriceView { Coins = [] });

    public ServiceResult<TokenPriceView> TokenPricesResult { get; set; } =
        ServiceResult<TokenPriceView>.Success(new TokenPriceView { Tokens = [] });

    public ServiceResult<CoinMarketChartView> MarketChartResult { get; set; } =
        ServiceResult<CoinMarketChartView>.Success(new CoinMarketChartView { Prices = [], MarketCaps = [], TotalVolumes = [] });

    public ServiceResult<CoinView> CoinResult { get; set; } =
        ServiceResult<CoinView>.Success(new CoinView { Id = "bitcoin", Symbol = "btc", Name = "Bitcoin" });

    public ServiceResult<CoinDeveloperDataView> DeveloperDataResult { get; set; } =
        ServiceResult<CoinDeveloperDataView>.Success(new CoinDeveloperDataView { Id = "bitcoin", Symbol = "btc", Name = "Bitcoin" });

    public Task<ServiceResult<CoinPriceView>> GetPricesAsync(CoinPriceQuery query, CancellationToken cancellationToken)
    {
        GetPricesCallCount++;
        CapturedCancellationToken = cancellationToken;
        return Task.FromResult(PricesResult);
    }

    public Task<ServiceResult<TokenPriceView>> GetTokenPricesAsync(TokenPriceQuery query, CancellationToken cancellationToken)
    {
        GetTokenPricesCallCount++;
        CapturedCancellationToken = cancellationToken;
        return Task.FromResult(TokenPricesResult);
    }

    public Task<ServiceResult<CoinMarketChartView>> GetMarketChartAsync(CoinMarketChartQuery query, CancellationToken cancellationToken)
    {
        GetMarketChartCallCount++;
        CapturedCancellationToken = cancellationToken;
        return Task.FromResult(MarketChartResult);
    }

    public Task<ServiceResult<CoinView>> GetCoinAsync(CoinQuery query, CancellationToken cancellationToken)
    {
        GetCoinCallCount++;
        CapturedCancellationToken = cancellationToken;
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return Task.FromResult(CoinResult);
    }

    public Task<ServiceResult<CoinDeveloperDataView>> GetDeveloperDataAsync(CoinDeveloperDataQuery query, CancellationToken cancellationToken)
    {
        GetDeveloperDataCallCount++;
        CapturedCancellationToken = cancellationToken;
        return Task.FromResult(DeveloperDataResult);
    }
}
