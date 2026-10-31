namespace CryptoStuff.Core.Watchlists;

/// <summary>A request to create an empty watchlist.</summary>
public sealed record CreateWatchlistCommand
{
    /// <summary>The new watchlist's name; must satisfy <see cref="NameValidator"/>.</summary>
    public required string Name { get; init; }
}
