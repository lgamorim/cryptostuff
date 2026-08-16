using CryptoStuff.CoinGecko;

namespace CryptoStuff.Core;

/// <summary>The default <see cref="ICryptocurrencyService"/>, backed by an <see cref="ICoinGeckoClient"/>.</summary>
public sealed class CryptocurrencyService(ICoinGeckoClient client) : ICryptocurrencyService
{
    /// <inheritdoc />
    public async Task<ServiceResult<CoinPriceView>> GetPricesAsync(CoinPriceQuery query, CancellationToken cancellationToken)
    {
        var response = await client.GetSimplePriceAsync(query.CoinIds, query.VsCurrencies, cancellationToken);
        if (!response.IsSuccess)
        {
            return ServiceResult<CoinPriceView>.Failure(ServiceErrorCodeMapper.Map(response));
        }

        return ToCollectionResult(response.Value, query.CoinIds, matrix => CoinPriceMapper.ToView(matrix, query.CoinIds, query.VsCurrencies));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<TokenPriceView>> GetTokenPricesAsync(TokenPriceQuery query, CancellationToken cancellationToken)
    {
        var response = await client.GetSimpleTokenPriceAsync(query.Platform, query.ContractAddresses, query.VsCurrencies, cancellationToken);
        if (!response.IsSuccess)
        {
            return ServiceResult<TokenPriceView>.Failure(ServiceErrorCodeMapper.Map(response));
        }

        return ToCollectionResult(response.Value, query.ContractAddresses, matrix => TokenPriceMapper.ToView(matrix, query.ContractAddresses, query.VsCurrencies));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<CoinMarketChartView>> GetMarketChartAsync(CoinMarketChartQuery query, CancellationToken cancellationToken)
    {
        var response = await client.GetMarketChartAsync(query.CoinId, query.VsCurrency, query.Days, cancellationToken);
        return ToServiceResult(response, CoinMarketChartMapper.ToView);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<CoinView>> GetCoinAsync(CoinQuery query, CancellationToken cancellationToken)
    {
        var response = await client.GetCoinAsync(query.CoinId, cancellationToken);
        return ToServiceResult(response, CoinMapper.ToView);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<CoinDeveloperDataView>> GetDeveloperDataAsync(CoinDeveloperDataQuery query, CancellationToken cancellationToken)
    {
        var response = await client.GetCoinHistoryAsync(query.CoinId, query.Date, cancellationToken);
        return ToServiceResult(response, CoinDeveloperDataMapper.ToView);
    }

    /// <summary>
    /// Maps a single-resource response to a result: a failed response maps
    /// through <see cref="ServiceErrorCodeMapper"/>; a successful response
    /// with a null body — CoinGecko said success but had nothing to say — is
    /// treated as <see cref="ServiceErrorCode.NotFound"/>; otherwise the body
    /// is mapped to its view.
    /// </summary>
    private static ServiceResult<TView> ToServiceResult<TValue, TView>(CoinGeckoResponse<TValue> response, Func<TValue, TView> map)
    {
        if (!response.IsSuccess)
        {
            return ServiceResult<TView>.Failure(ServiceErrorCodeMapper.Map(response));
        }

        return response.Value is null
            ? ServiceResult<TView>.Failure(ServiceErrorCode.NotFound)
            : ServiceResult<TView>.Success(map(response.Value));
    }

    /// <summary>
    /// Maps a price-matrix response to a result: a null matrix is treated as
    /// empty, then any requested identifier absent from the (possibly empty)
    /// matrix is a <see cref="ServiceErrorCode.NotFound"/> failure for the
    /// whole request — CoinGecko silently omits unknown identifiers rather
    /// than erroring. An empty identifier request always succeeds with an
    /// empty view, even against a null matrix.
    /// </summary>
    private static ServiceResult<TView> ToCollectionResult<TView>(
        Dictionary<string, Dictionary<string, decimal>>? matrix,
        IReadOnlyList<string> identifiers,
        Func<IReadOnlyDictionary<string, Dictionary<string, decimal>>, TView> map)
    {
        var resolvedMatrix = matrix ?? new Dictionary<string, Dictionary<string, decimal>>();
        return identifiers.Any(id => !resolvedMatrix.ContainsKey(id))
            ? ServiceResult<TView>.Failure(ServiceErrorCode.NotFound)
            : ServiceResult<TView>.Success(map(resolvedMatrix));
    }
}
