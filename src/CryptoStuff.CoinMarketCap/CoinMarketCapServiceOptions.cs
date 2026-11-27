namespace CryptoStuff.CoinMarketCap;

/// <summary>Configures what <see cref="CoinMarketCapCryptocurrencyService"/> asks CoinMarketCap for.</summary>
public sealed record CoinMarketCapServiceOptions
{
    /// <summary>
    /// The currencies a coin's detail is quoted in. A coin query names no
    /// currency, and CoinMarketCap's Basic plan converts to one currency per
    /// call, so each one listed costs a call. Defaults to <c>usd</c> alone.
    /// </summary>
    public IReadOnlyList<string> DetailCurrencies { get; init; } = ["usd"];
}
