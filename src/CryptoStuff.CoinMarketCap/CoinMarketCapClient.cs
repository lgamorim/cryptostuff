using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CryptoStuff.Core;

namespace CryptoStuff.CoinMarketCap;

/// <inheritdoc cref="ICoinMarketCapClient" />
public sealed class CoinMarketCapClient(HttpClient httpClient, IMeterFactory? meterFactory = null) : ICoinMarketCapClient
{
    /// <summary>The <c>provider</c> tag value on every upstream-call measurement about CoinMarketCap.</summary>
    public const string ProviderName = "coinmarketcap";

    private readonly Counter<long>? _requests =
        meterFactory?.Create(UpstreamMetrics.MeterName).CreateCounter<long>("cryptostuff.upstream.requests", "{request}");

    /// <inheritdoc />
    /// <remarks>
    /// Deserializes with <see cref="JsonSerializerDefaults.Web"/> options —
    /// case-insensitive property matching, a camelCase naming policy, and
    /// numbers read from JSON strings too. A wire type's property bound to a
    /// snake_case field (e.g. <c>volume_24h</c>) still needs an explicit
    /// <c>[JsonPropertyName]</c>.
    /// <para>
    /// A failure's body is read for CoinMarketCap's <c>status.error_code</c>
    /// and <c>status.error_message</c>; a body that is empty, not JSON, or
    /// carries no <c>status</c> leaves both null rather than throwing, since
    /// a proxy or gateway in between may answer with anything. A send that
    /// gets no HTTP response at all (an <see cref="HttpRequestException"/>:
    /// connection refused, DNS or TLS failure, connection dropped) is
    /// returned as <see cref="CoinMarketCapResponse{TValue}.NoResponse"/>
    /// rather than thrown. A successful status whose body is not valid JSON
    /// still throws.
    /// </para>
    /// <para>
    /// Each call adds one to the <c>cryptostuff.upstream.requests</c> counter
    /// (when a meter factory was given), however it ends — a call the retry
    /// handler resends after a <c>429</c> is still one request. Tags:
    /// <c>provider</c> (<see cref="ProviderName"/>), <c>endpoint</c> (the
    /// endpoint's path, or <c>other</c>) and <c>outcome</c> (<c>success</c>,
    /// <c>not_found</c>, <c>rate_limited</c>, <c>client_error</c>,
    /// <c>server_error</c>, <c>other_status</c>, <c>timeout</c>,
    /// <c>no_response</c>, <c>cancelled</c> or <c>exception</c>), the values
    /// CoinGecko's client records too.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="requestUri"/> is null. Nothing is sent, and no request
    /// is recorded.
    /// </exception>
    public async Task<CoinMarketCapResponse<TValue>> GetAsync<TValue>(string requestUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestUri);

        // Stays "exception" only when SendAsync throws anything but the
        // caller's own cancellation: a successful body that failed to
        // deserialize, or anything else unexpected.
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
                new KeyValuePair<string, object?>("endpoint", CoinMarketCapEndpoint.PathOf(requestUri)),
                new KeyValuePair<string, object?>("outcome", outcome));
        }
    }

    private async Task<CoinMarketCapResponse<TValue>> SendAsync<TValue>(string requestUri, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(requestUri, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CoinMarketCapResponse<TValue>.Timeout();
        }
        catch (HttpRequestException)
        {
            return CoinMarketCapResponse<TValue>.NoResponse();
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var status = await ReadErrorStatusAsync(response.Content, cancellationToken);
                return CoinMarketCapResponse<TValue>.Failure(response.StatusCode, status?.ErrorCode, status?.ErrorMessage);
            }

            var envelope = await response.Content.ReadFromJsonAsync<CoinMarketCapEnvelope<TValue>>(cancellationToken);
            return CoinMarketCapResponse<TValue>.Success(envelope is null ? default : envelope.Data, response.StatusCode);
        }
    }

    private static string OutcomeOf<TValue>(CoinMarketCapResponse<TValue> response) => response switch
    {
        { IsSuccess: true } => "success",
        { IsTimeout: true } => "timeout",
        { StatusCode: null } => "no_response",
        { StatusCode: HttpStatusCode.NotFound } => "not_found",
        { StatusCode: HttpStatusCode.TooManyRequests } => "rate_limited",
        { StatusCode: >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError } => "client_error",
        { StatusCode: >= HttpStatusCode.InternalServerError and <= (HttpStatusCode)599 } => "server_error",
        _ => "other_status",
    };

    private static async Task<CoinMarketCapStatus?> ReadErrorStatusAsync(HttpContent content, CancellationToken cancellationToken)
    {
        var body = await content.ReadAsStringAsync(cancellationToken);
        try
        {
            return JsonSerializer.Deserialize<CoinMarketCapErrorBody>(body, JsonSerializerOptions.Web)?.Status;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
