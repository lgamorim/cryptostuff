namespace CryptoStuff.Core.Watchlists;

/// <summary>
/// The watchlist workflow both hosts drive. An unknown watchlist id is
/// <see cref="ServiceErrorCode.NotFound"/>; a write that conflicts with stored
/// state is <see cref="ServiceErrorCode.Conflict"/>. Names and coin ids are
/// expected to have passed the shared validators already: one that breaks the
/// entity's rules throws <see cref="ArgumentException"/>.
/// </summary>
public interface IWatchlistService
{
    /// <summary>Gets every watchlist, ordered by id.</summary>
    Task<ServiceResult<IReadOnlyList<WatchlistView>>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Gets one watchlist.</summary>
    Task<ServiceResult<WatchlistView>> GetAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Creates an empty watchlist. A name already taken by another watchlist,
    /// compared case-insensitively, is a <see cref="ServiceErrorCode.Conflict"/>.
    /// </summary>
    Task<ServiceResult<WatchlistView>> CreateAsync(CreateWatchlistCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Renames a watchlist. A name already taken by another watchlist,
    /// compared case-insensitively, is a <see cref="ServiceErrorCode.Conflict"/>.
    /// </summary>
    Task<ServiceResult<WatchlistView>> RenameAsync(RenameWatchlistCommand command, CancellationToken cancellationToken);

    /// <summary>Deletes a watchlist.</summary>
    Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Appends a coin, after checking with CoinGecko that it exists. A coin
    /// CoinGecko doesn't know is a <see cref="ServiceErrorCode.RuleViolated"/>
    /// naming it; any other upstream failure propagates. Adding a coin already
    /// present succeeds without changing anything or calling CoinGecko.
    /// </summary>
    Task<ServiceResult<WatchlistView>> AddCoinAsync(WatchlistCoinCommand command, CancellationToken cancellationToken);

    /// <summary>Removes a coin. Removing a coin that is absent succeeds without changing anything.</summary>
    Task<ServiceResult<WatchlistView>> RemoveCoinAsync(WatchlistCoinCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Gets live prices for the watchlist's coins, in its order, through
    /// <see cref="ICryptocurrencyService.GetPricesAsync"/>, whose failures
    /// propagate, except that a coin CoinGecko does not know is a
    /// <see cref="ServiceErrorCode.UpstreamUnavailable"/> rather than a
    /// <see cref="ServiceErrorCode.NotFound"/>, which stays for an unknown
    /// watchlist. An empty watchlist gets an empty view without calling CoinGecko.
    /// </summary>
    Task<ServiceResult<CoinPriceView>> GetPricesAsync(WatchlistPriceQuery query, CancellationToken cancellationToken);
}
