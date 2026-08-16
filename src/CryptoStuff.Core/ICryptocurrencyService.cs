namespace CryptoStuff.Core;

/// <summary>
/// The domain-level cryptocurrency market data workflow, decoupled from
/// CoinGecko's wire format. Implemented by <see cref="CryptocurrencyService"/>;
/// milestone `3.4`'s caching decorator depends on this abstraction instead of
/// the concrete type, so further decoration stays possible.
/// </summary>
public interface ICryptocurrencyService
{
    /// <summary>Gets live prices for the requested coins in the requested currencies.</summary>
    Task<ServiceResult<CoinPriceView>> GetPricesAsync(CoinPriceQuery query, CancellationToken cancellationToken);

    /// <summary>Gets live prices for the requested token contract addresses in the requested currencies.</summary>
    Task<ServiceResult<TokenPriceView>> GetTokenPricesAsync(TokenPriceQuery query, CancellationToken cancellationToken);

    /// <summary>Gets the historical price, market cap, and volume series for a coin.</summary>
    Task<ServiceResult<CoinMarketChartView>> GetMarketChartAsync(CoinMarketChartQuery query, CancellationToken cancellationToken);

    /// <summary>Gets coin detail: description, image, and current market data.</summary>
    Task<ServiceResult<CoinView>> GetCoinAsync(CoinQuery query, CancellationToken cancellationToken);

    /// <summary>Gets a coin's developer repository activity as of a given date.</summary>
    Task<ServiceResult<CoinDeveloperDataView>> GetDeveloperDataAsync(CoinDeveloperDataQuery query, CancellationToken cancellationToken);
}
