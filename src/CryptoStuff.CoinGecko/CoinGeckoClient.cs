using System.Net.Http.Json;

namespace CryptoStuff.CoinGecko;

/// <inheritdoc cref="ICoinGeckoClient" />
public sealed class CoinGeckoClient(HttpClient httpClient) : ICoinGeckoClient
{
    /// <inheritdoc />
    /// <remarks>
    /// Deserializes with <c>HttpContent.ReadFromJsonAsync</c>'s implicit
    /// options, which are <see cref="System.Text.Json.JsonSerializerDefaults.Web"/> —
    /// case-insensitive property matching plus a camelCase naming policy, not
    /// <see cref="System.Text.Json.JsonSerializerOptions.Default"/>. A DTO
    /// property bound to a snake_case field (e.g. <c>market_caps</c>) still
    /// needs an explicit <c>[JsonPropertyName]</c>, since neither camelCase
    /// matches snake_case.
    /// </remarks>
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
