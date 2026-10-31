namespace CryptoStuff.Core.Watchlists;

/// <summary>A watchlist as the hosts show it.</summary>
public sealed record WatchlistView
{
    /// <summary>The watchlist's id.</summary>
    public required int Id { get; init; }

    /// <summary>The watchlist's name.</summary>
    public required string Name { get; init; }

    /// <summary>The watchlist's lowercased coin ids, in the order they were added.</summary>
    public required IReadOnlyList<string> CoinIds { get; init; }
}
