namespace CryptoStuff.CoinGecko;

/// <summary>
/// Sends authenticated GET requests to CoinGecko and deserializes the JSON
/// response.
/// </summary>
/// <remarks>
/// This interface intentionally stays generic. Endpoint-specific calls (e.g.
/// simple price, market chart) should be added as extension methods that
/// compose <see cref="GetAsync{TValue}"/> with request-URI building, rather
/// than as new interface members — keeping the interface's single
/// responsibility from growing into a multi-capability contract.
/// </remarks>
public interface ICoinGeckoClient
{
    /// <summary>
    /// Sends a GET request to <paramref name="requestUri"/> (relative to the
    /// configured base address) and deserializes the JSON response as
    /// <typeparamref name="TValue"/>.
    /// </summary>
    Task<CoinGeckoResponse<TValue>> GetAsync<TValue>(string requestUri, CancellationToken cancellationToken);
}
