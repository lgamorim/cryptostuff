namespace CryptoStuff.Core;

/// <summary>
/// The domain-level cryptocurrency market data workflow, decoupled from any
/// provider's wire format. Implemented by <c>CryptocurrencyService</c> in
/// <c>CryptoStuff.CoinGecko</c> and by <c>CoinMarketCapCryptocurrencyService</c>
/// in <c>CryptoStuff.CoinMarketCap</c>, each adapting its provider to this
/// contract the way <c>CryptoStuff.Persistence</c> adapts EF Core to the
/// repository contracts; milestone `3.4`'s caching decorator depends on this
/// abstraction instead of a concrete type, so further decoration stays possible.
/// </summary>
public interface ICryptocurrencyService
{
    /// <summary>Gets live prices for the requested coins in the requested currencies.</summary>
    /// <remarks>
    /// All or nothing: when the market data provider does not know any one
    /// requested coin, the whole call fails with
    /// <see cref="ServiceErrorCode.NotFound"/>, without naming the coin, and no
    /// other coin's price comes back; <see cref="KnownCoinPrices.GetKnownCoinPricesAsync"/>
    /// tells the known coins from the unknown ones. A currency the provider
    /// does not quote a known coin in is no failure: the coin comes back
    /// without an amount in it. A query naming no coins succeeds with none.
    /// </remarks>
    Task<ServiceResult<CoinPriceView>> GetPricesAsync(CoinPriceQuery query, CancellationToken cancellationToken);

    /// <summary>Gets live prices for the requested token contract addresses in the requested currencies.</summary>
    /// <remarks>
    /// All or nothing, as <see cref="GetPricesAsync"/> is: when the market data
    /// provider does not know any one requested address on the platform, the
    /// whole call fails with <see cref="ServiceErrorCode.NotFound"/>, without
    /// naming the address. A currency the provider does not quote a known
    /// token in is no failure: the token comes back without an amount in it. A
    /// query naming no addresses succeeds with none.
    /// </remarks>
    Task<ServiceResult<TokenPriceView>> GetTokenPricesAsync(TokenPriceQuery query, CancellationToken cancellationToken);

    /// <summary>Gets the historical price, market cap, and volume series for a coin.</summary>
    /// <remarks>
    /// A provider whose plan serves fewer days of history than the query asks
    /// for fails with <see cref="ServiceErrorCode.UpstreamUnavailable"/>.
    /// </remarks>
    Task<ServiceResult<CoinMarketChartView>> GetMarketChartAsync(CoinMarketChartQuery query, CancellationToken cancellationToken);

    /// <summary>Gets coin detail: description, image, and current market data.</summary>
    Task<ServiceResult<CoinView>> GetCoinAsync(CoinQuery query, CancellationToken cancellationToken);

    /// <summary>Gets a coin's developer repository activity as of a given date.</summary>
    /// <remarks>
    /// A provider with no developer data fails with
    /// <see cref="ServiceErrorCode.Unsupported"/>, naming itself in the detail.
    /// </remarks>
    Task<ServiceResult<CoinDeveloperDataView>> GetDeveloperDataAsync(CoinDeveloperDataQuery query, CancellationToken cancellationToken);
}
