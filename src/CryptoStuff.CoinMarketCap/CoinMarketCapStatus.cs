using System.Text.Json.Serialization;

namespace CryptoStuff.CoinMarketCap;

/// <summary>
/// The <c>status</c> object CoinMarketCap puts in every response body. Only
/// the error fields are read. <c>error_code</c> is <c>0</c> on success; some
/// endpoints send it as a JSON string rather than a number, which the
/// web-default serializer options read as a number all the same.
/// </summary>
internal sealed record CoinMarketCapStatus
{
    [JsonPropertyName("error_code")]
    public int? ErrorCode { get; init; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }
}
