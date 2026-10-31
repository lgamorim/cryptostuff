namespace CryptoStuff.Core.Watchlists;

/// <summary>A request for live prices of every coin in a watchlist.</summary>
public sealed record WatchlistPriceQuery
{
    /// <summary>The id of the watchlist to price.</summary>
    public required int WatchlistId { get; init; }

    /// <summary>The vs_currency code to price each coin in.</summary>
    public required string VsCurrency { get; init; }
}
