using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using CryptoStuff.Core;

namespace CryptoStuff.CoinGecko;

/// <inheritdoc cref="ICoinGeckoClient" />
public sealed class CoinGeckoClient(HttpClient httpClient, IMeterFactory? meterFactory = null) : ICoinGeckoClient
{
    /// <summary>The <c>provider</c> tag value on every upstream-call measurement about CoinGecko.</summary>
    public const string ProviderName = "coingecko";

    private readonly Counter<long>? _requests =
        meterFactory?.Create(UpstreamMetrics.MeterName).CreateCounter<long>("cryptostuff.upstream.requests", "{request}");

    /// <inheritdoc />
    /// <remarks>
    /// Deserializes with <c>HttpContent.ReadFromJsonAsync</c>'s implicit
    /// options, which are <see cref="System.Text.Json.JsonSerializerDefaults.Web"/> —
    /// case-insensitive property matching plus a camelCase naming policy, not
    /// <see cref="System.Text.Json.JsonSerializerOptions.Default"/>. A DTO
    /// property bound to a snake_case field (e.g. <c>market_caps</c>) still
    /// needs an explicit <c>[JsonPropertyName]</c>, since neither camelCase
    /// matches snake_case.
    /// <para>
    /// Each call adds one to the <c>cryptostuff.upstream.requests</c> counter
    /// (when a meter factory was given), however it ends — a call the retry
    /// handler resends after a <c>429</c> is still one request. Tags:
    /// <c>provider</c> (<see cref="ProviderName"/>), <c>endpoint</c> (the
    /// template the URI matches, or <c>other</c>) and <c>outcome</c>
    /// (<c>success</c>, <c>not_found</c>, <c>rate_limited</c>,
    /// <c>client_error</c>, <c>server_error</c>, <c>other_status</c>,
    /// <c>timeout</c>, <c>cancelled</c> or <c>exception</c>).
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="requestUri"/> is null. Nothing is sent, and no request
    /// is recorded.
    /// </exception>
    public async Task<CoinGeckoResponse<TValue>> GetAsync<TValue>(string requestUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestUri);

        // Stays "exception" only when SendAsync throws anything but the
        // caller's own cancellation: a send that never got a response, or a
        // successful body that failed to deserialize.
        var outcome = "exception";
        try
        {
            var response = await SendAsync<TValue>(requestUri, cancellationToken);
            outcome = OutcomeOf(response);
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "cancelled";
            throw;
        }
        finally
        {
            _requests?.Add(
                1,
                new KeyValuePair<string, object?>("provider", ProviderName),
                new KeyValuePair<string, object?>("endpoint", CoinGeckoEndpoint.TemplateOf(requestUri)),
                new KeyValuePair<string, object?>("outcome", outcome));
        }
    }

    private async Task<CoinGeckoResponse<TValue>> SendAsync<TValue>(string requestUri, CancellationToken cancellationToken)
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

    private static string OutcomeOf<TValue>(CoinGeckoResponse<TValue> response) => response switch
    {
        { IsSuccess: true } => "success",
        { IsTimeout: true } => "timeout",
        { StatusCode: HttpStatusCode.NotFound } => "not_found",
        { StatusCode: HttpStatusCode.TooManyRequests } => "rate_limited",
        { StatusCode: >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError } => "client_error",
        { StatusCode: >= HttpStatusCode.InternalServerError and <= (HttpStatusCode)599 } => "server_error",
        _ => "other_status",
    };
}
