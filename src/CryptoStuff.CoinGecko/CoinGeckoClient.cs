using System.Net.Http.Json;

namespace CryptoStuff.CoinGecko;

/// <inheritdoc cref="ICoinGeckoClient" />
public sealed class CoinGeckoClient(HttpClient httpClient) : ICoinGeckoClient
{
    /// <inheritdoc />
    public async Task<CoinGeckoResponse<TValue>> GetAsync<TValue>(string requestUri, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(requestUri, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CoinGeckoResponse<TValue>.Timeout();
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return CoinGeckoResponse<TValue>.Failure(response.StatusCode);
            }

            var value = await response.Content.ReadFromJsonAsync<TValue>(cancellationToken);
            return CoinGeckoResponse<TValue>.Success(value, response.StatusCode);
        }
    }
}
