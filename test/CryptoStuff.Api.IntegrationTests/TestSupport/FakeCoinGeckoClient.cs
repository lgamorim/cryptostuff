using CryptoStuff.CoinGecko;

namespace CryptoStuff.Api.IntegrationTests.TestSupport;

/// <summary>
/// The one seam substituted for these integration tests: everything above
/// <see cref="ICoinGeckoClient"/> (service, caching, validation, routing) is
/// real. Each test configures only the response(s) its scenario needs.
/// </summary>
internal sealed class FakeCoinGeckoClient : ICoinGeckoClient
{
    public CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>? PriceMatrixResponse { get; set; }

    public CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>? TokenPriceMatrixResponse { get; set; }

    public CoinGeckoResponse<CoinGeckoMarketChart>? MarketChartResponse { get; set; }

    public CoinGeckoResponse<CoinGeckoCoin>? CoinResponse { get; set; }

    public CoinGeckoResponse<CoinGeckoCoinHistory>? CoinHistoryResponse { get; set; }

    /// <summary>How many times <see cref="GetAsync{TValue}"/> was called — lets a test prove the caching decorator actually short-circuits a repeat request.</summary>
    public int CallCount { get; private set; }

    public Task<CoinGeckoResponse<TValue>> GetAsync<TValue>(string requestUri, CancellationToken cancellationToken)
    {
        CallCount++;

        object? response = requestUri switch
        {
            _ when requestUri.StartsWith("simple/price?", StringComparison.Ordinal) => PriceMatrixResponse,
            _ when requestUri.StartsWith("simple/token_price/", StringComparison.Ordinal) => TokenPriceMatrixResponse,
            _ when requestUri.Contains("/market_chart?", StringComparison.Ordinal) => MarketChartResponse,
            _ when requestUri.Contains("/history?", StringComparison.Ordinal) => CoinHistoryResponse,
            _ when requestUri.StartsWith("coins/", StringComparison.Ordinal) => CoinResponse,
            _ => null,
        };

        if (response is null)
        {
            throw new InvalidOperationException(
                $"No fake CoinGecko response configured for request URI '{requestUri}'. " +
                "Set the matching *Response property before issuing the request.");
        }

        if (response is not CoinGeckoResponse<TValue> typedResponse)
        {
            throw new InvalidOperationException(
                $"The response configured for request URI '{requestUri}' is a {response.GetType()}, " +
                $"but the caller expected a {typeof(CoinGeckoResponse<TValue>)}. " +
                "This usually means the wrong *Response property was set for the route under test.");
        }

        return Task.FromResult(typedResponse);
    }
}
