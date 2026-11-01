namespace CryptoStuff.Api.Watchlists;

/// <summary>The JSON body of <c>POST /watchlists</c> and <c>PUT /watchlists/{id}</c>.</summary>
public sealed record WatchlistRequest
{
    /// <summary>The watchlist's name.</summary>
    public string? Name { get; init; }
}
