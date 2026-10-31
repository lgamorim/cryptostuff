namespace CryptoStuff.Core.Watchlists;

/// <summary>A request to add a coin to, or remove one from, a watchlist.</summary>
public sealed record WatchlistCoinCommand
{
    /// <summary>The id of the watchlist to change.</summary>
    public required int WatchlistId { get; init; }

    /// <summary>The CoinGecko coin id, in any case; it is stored lowercased.</summary>
    public required string CoinId { get; init; }
}
