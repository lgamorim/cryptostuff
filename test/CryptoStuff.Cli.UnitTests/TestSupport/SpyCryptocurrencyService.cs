using CryptoStuff.Core;

namespace CryptoStuff.Cli.UnitTests.TestSupport;

internal sealed class SpyCryptocurrencyService : ICryptocurrencyService
{
    public CoinPriceQuery? CapturedPricesQuery { get; private set; }

    public TokenPriceQuery? CapturedTokenPricesQuery { get; private set; }

    public CoinMarketChartQuery? CapturedMarketChartQuery { get; private set; }

    public CoinQuery? CapturedCoinQuery { get; private set; }

    public CoinDeveloperDataQuery? CapturedDeveloperDataQuery { get; private set; }

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
        CapturedPricesQuery = query;
        CapturedCancellationToken = cancellationToken;
        ThrowIfConfigured();
        return Task.FromResult(PricesResult);
    }

    public Task<ServiceResult<TokenPriceView>> GetTokenPricesAsync(TokenPriceQuery query, CancellationToken cancellationToken)
    {
        CapturedTokenPricesQuery = query;
        CapturedCancellationToken = cancellationToken;
        ThrowIfConfigured();
        return Task.FromResult(TokenPricesResult);
    }

    public Task<ServiceResult<CoinMarketChartView>> GetMarketChartAsync(CoinMarketChartQuery query, CancellationToken cancellationToken)
    {
        CapturedMarketChartQuery = query;
        CapturedCancellationToken = cancellationToken;
        ThrowIfConfigured();
        return Task.FromResult(MarketChartResult);
    }

    public Task<ServiceResult<CoinView>> GetCoinAsync(CoinQuery query, CancellationToken cancellationToken)
    {
        CapturedCoinQuery = query;
        CapturedCancellationToken = cancellationToken;
        ThrowIfConfigured();
        return Task.FromResult(CoinResult);
    }

    public Task<ServiceResult<CoinDeveloperDataView>> GetDeveloperDataAsync(CoinDeveloperDataQuery query, CancellationToken cancellationToken)
    {
        CapturedDeveloperDataQuery = query;
        CapturedCancellationToken = cancellationToken;
        ThrowIfConfigured();
        return Task.FromResult(DeveloperDataResult);
    }

    private void ThrowIfConfigured()
    {
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }
    }
}
