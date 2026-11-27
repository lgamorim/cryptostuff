namespace CryptoStuff.CoinMarketCap;

/// <summary>A failed CoinMarketCap body, read for its <c>status</c> alone.</summary>
internal sealed record CoinMarketCapErrorBody
{
    public CoinMarketCapStatus? Status { get; init; }
}
