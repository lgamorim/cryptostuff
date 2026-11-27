using System.Text.Json.Serialization;

namespace CryptoStuff.CoinMarketCap;

/// <summary>
/// One coin in the <c>data</c> array <c>v3/cryptocurrency/quotes/latest</c>
/// returns, with its quote in each requested currency. The <c>required</c>
/// members, here and in <see cref="CoinMarketCapCurrencyQuote"/>, are the ones
/// a caller reads; a body that lacks one fails to deserialize.
/// An explicit JSON <c>null</c> in place of a required member is rejected for
/// a value-type one such as <c>Id</c>, but accepted for a reference-type one,
/// which is then left null despite its declared type.
/// </summary>
public sealed record CoinMarketCapLatestQuote
{
    /// <summary>CoinMarketCap's numeric id for the coin, which <c>quotes/historical</c> takes.</summary>
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    /// <summary>The coin's slug, the identifier it was requested by.</summary>
    [JsonPropertyName("slug")]
    public required string Slug { get; init; }

    /// <summary>The coin's quote in each requested currency.</summary>
    [JsonPropertyName("quote")]
    public required IReadOnlyList<CoinMarketCapCurrencyQuote> Quote { get; init; }
}
