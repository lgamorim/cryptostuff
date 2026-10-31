namespace CryptoStuff.Core.Watchlists;

/// <summary>A request to give a watchlist a new name.</summary>
public sealed record RenameWatchlistCommand
{
    /// <summary>The id of the watchlist to rename.</summary>
    public required int WatchlistId { get; init; }

    /// <summary>The new name; must satisfy <see cref="NameValidator"/>.</summary>
    public required string Name { get; init; }
}
