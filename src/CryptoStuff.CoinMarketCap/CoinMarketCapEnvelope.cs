namespace CryptoStuff.CoinMarketCap;

/// <summary>
/// A successful CoinMarketCap body, read for its <c>data</c> alone. Its
/// <c>status</c> is not read, so an unexpected shape there cannot cost a
/// call whose <c>data</c> is fine.
/// </summary>
internal sealed record CoinMarketCapEnvelope<TValue>
{
    public TValue? Data { get; init; }
}
