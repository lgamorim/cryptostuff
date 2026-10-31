namespace CryptoStuff.Core.Watchlists;

/// <summary>Maps a <see cref="Watchlist"/> to a <see cref="WatchlistView"/>.</summary>
public static class WatchlistMapper
{
    /// <summary>
    /// Maps <paramref name="watchlist"/> to a view holding a copy of its coin
    /// ids, so later changes to the entity never show through a view already
    /// handed out.
    /// </summary>
    public static WatchlistView ToView(Watchlist watchlist) =>
        new() { Id = watchlist.Id, Name = watchlist.Name, CoinIds = [.. watchlist.CoinIds] };
}
