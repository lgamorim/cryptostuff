namespace CryptoStuff.CoinMarketCap;

/// <summary>
/// Sends authenticated GET requests to CoinMarketCap and deserializes the
/// <c>data</c> of the JSON response.
/// </summary>
/// <remarks>
/// This interface intentionally stays generic. Endpoint-specific calls (e.g.
/// latest quotes, coin info) are extension methods that compose
/// <see cref="GetAsync{TValue}"/> with request-URI building, rather than new
/// interface members — keeping the interface's single responsibility from
/// growing into a multi-capability contract.
/// </remarks>
public interface ICoinMarketCapClient
{
    /// <summary>
    /// Sends a GET request to <paramref name="requestUri"/> (relative to the
    /// configured base address) and deserializes the response's <c>data</c>
    /// as <typeparamref name="TValue"/>.
    /// </summary>
    Task<CoinMarketCapResponse<TValue>> GetAsync<TValue>(string requestUri, CancellationToken cancellationToken);
}
