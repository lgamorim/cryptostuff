using System.Text.Json.Serialization;

namespace CryptoStuff.CoinMarketCap;

/// <summary>
/// One coin's quote history, as <c>v3/cryptocurrency/quotes/historical</c>
/// returns it under the id it was requested by — keyed even for a single id,
/// which this version always does. The <c>required</c> members, here and in
/// <see cref="CoinMarketCapHistoricalQuote"/>, are the ones a caller reads; a
/// body that lacks one fails to deserialize. An explicit JSON <c>null</c> in
/// place of one is rejected for the value-type <c>Timestamp</c>, but accepted
/// for a reference-type one, which is then left null despite its declared type.
/// </summary>
public sealed record CoinMarketCapHistoricalCoin
{
    /// <summary>The coin's quotes, one per interval, oldest first.</summary>
    [JsonPropertyName("quotes")]
    public required IReadOnlyList<CoinMarketCapHistoricalQuote> Quotes { get; init; }
}
