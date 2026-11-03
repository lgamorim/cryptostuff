namespace CryptoStuff.Core.Watchlists;

/// <summary>
/// The default <see cref="IWatchlistService"/>, storing watchlists through an
/// <see cref="IWatchlistRepository"/> and checking and pricing coins through
/// an <see cref="ICryptocurrencyService"/>.
/// </summary>
public sealed class WatchlistService(IWatchlistRepository repository, ICryptocurrencyService cryptocurrencyService) : IWatchlistService
{
    /// <inheritdoc />
    public async Task<ServiceResult<IReadOnlyList<WatchlistView>>> ListAsync(CancellationToken cancellationToken)
    {
        var watchlists = await repository.ListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<WatchlistView>>.Success(watchlists.Select(WatchlistMapper.ToView).ToArray());
    }

    /// <inheritdoc />
    public async Task<ServiceResult<WatchlistView>> GetAsync(int id, CancellationToken cancellationToken)
    {
        var watchlist = await repository.FindAsync(id, cancellationToken);
        return watchlist is null ? NotFound<WatchlistView>(id) : Success(watchlist);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<WatchlistView>> CreateAsync(CreateWatchlistCommand command, CancellationToken cancellationToken)
    {
        var watchlist = Watchlist.Create(command.Name);
        if (await repository.NameExistsAsync(command.Name, excludingId: null, cancellationToken))
        {
            return NameTaken(command.Name);
        }

        repository.Add(watchlist);
        return await TrySaveChangesAsync(cancellationToken) ? Success(watchlist) : NameTaken(command.Name);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<WatchlistView>> RenameAsync(RenameWatchlistCommand command, CancellationToken cancellationToken)
    {
        var watchlist = await repository.FindAsync(command.WatchlistId, cancellationToken);
        if (watchlist is null)
        {
            return NotFound<WatchlistView>(command.WatchlistId);
        }

        if (await repository.NameExistsAsync(command.Name, command.WatchlistId, cancellationToken))
        {
            return NameTaken(command.Name);
        }

        watchlist.Rename(command.Name);

        // A conflict here is most likely another watchlist taking the name
        // between the check above and this save — the race this path exists for.
        return await TrySaveChangesAsync(cancellationToken) ? Success(watchlist) : NameTaken(command.Name);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var watchlist = await repository.FindAsync(id, cancellationToken);
        if (watchlist is null)
        {
            return ServiceResult.Failure(ServiceErrorCode.NotFound, NotFoundDetail(id));
        }

        repository.Remove(watchlist);
        return await TrySaveChangesAsync(cancellationToken)
            ? ServiceResult.Success()
            : ServiceResult.Failure(ServiceErrorCode.Conflict, ChangedConcurrentlyDetail(id));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<WatchlistView>> AddCoinAsync(WatchlistCoinCommand command, CancellationToken cancellationToken)
    {
        var watchlist = await repository.FindAsync(command.WatchlistId, cancellationToken);
        if (watchlist is null)
        {
            return NotFound<WatchlistView>(command.WatchlistId);
        }

        var coinId = Watchlist.NormalizeCoinId(command.CoinId);
        if (watchlist.Contains(coinId))
        {
            return Success(watchlist);
        }

        var coin = await cryptocurrencyService.GetCoinAsync(new CoinQuery { CoinId = coinId }, cancellationToken);
        if (coin.ErrorCode is { } errorCode)
        {
            return errorCode == ServiceErrorCode.NotFound
                ? ServiceResult<WatchlistView>.Failure(ServiceErrorCode.RuleViolated, $"CoinGecko has no coin with id '{coinId}'.")
                : ServiceResult<WatchlistView>.Failure(errorCode, coin.ErrorDetail);
        }

        watchlist.AddCoin(coinId);
        return await SaveCoinChangeAsync(watchlist, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<WatchlistView>> RemoveCoinAsync(WatchlistCoinCommand command, CancellationToken cancellationToken)
    {
        var watchlist = await repository.FindAsync(command.WatchlistId, cancellationToken);
        if (watchlist is null)
        {
            return NotFound<WatchlistView>(command.WatchlistId);
        }

        return watchlist.RemoveCoin(command.CoinId)
            ? await SaveCoinChangeAsync(watchlist, cancellationToken)
            : Success(watchlist);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<CoinPriceView>> GetPricesAsync(WatchlistPriceQuery query, CancellationToken cancellationToken)
    {
        var watchlist = await repository.FindAsync(query.WatchlistId, cancellationToken);
        if (watchlist is null)
        {
            return NotFound<CoinPriceView>(query.WatchlistId);
        }

        if (watchlist.CoinIds.Count == 0)
        {
            return ServiceResult<CoinPriceView>.Success(new CoinPriceView { Coins = [] });
        }

        var priceQuery = new CoinPriceQuery { CoinIds = [.. watchlist.CoinIds], VsCurrencies = [query.VsCurrency] };
        var prices = await cryptocurrencyService.GetPricesAsync(priceQuery, cancellationToken);

        // The watchlist exists, so a coin CoinGecko no longer knows is an
        // upstream problem, not a missing watchlist.
        return prices.ErrorCode == ServiceErrorCode.NotFound
            ? ServiceResult<CoinPriceView>.Failure(
                ServiceErrorCode.UpstreamUnavailable,
                $"CoinGecko does not know one of the watchlist's coins: {string.Join(", ", watchlist.CoinIds)}.")
            : prices;
    }

    private async Task<ServiceResult<WatchlistView>> SaveCoinChangeAsync(Watchlist watchlist, CancellationToken cancellationToken) =>
        await TrySaveChangesAsync(cancellationToken)
            ? Success(watchlist)
            : ServiceResult<WatchlistView>.Failure(ServiceErrorCode.Conflict, ChangedConcurrentlyDetail(watchlist.Id));

    /// <summary>
    /// Saves pending changes, returning false when the repository reports a
    /// <see cref="PersistenceConflictException"/>. Any other exception,
    /// including cancellation, propagates.
    /// </summary>
    private async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (PersistenceConflictException)
        {
            return false;
        }
    }

    private static ServiceResult<WatchlistView> Success(Watchlist watchlist) =>
        ServiceResult<WatchlistView>.Success(WatchlistMapper.ToView(watchlist));

    private static ServiceResult<TValue> NotFound<TValue>(int id) =>
        ServiceResult<TValue>.Failure(ServiceErrorCode.NotFound, NotFoundDetail(id));

    private static ServiceResult<WatchlistView> NameTaken(string name) =>
        ServiceResult<WatchlistView>.Failure(ServiceErrorCode.Conflict, $"A watchlist named '{name}' already exists.");

    private static string NotFoundDetail(int id) => $"No watchlist has id {id}.";

    private static string ChangedConcurrentlyDetail(int id) =>
        $"Watchlist {id} was changed by another operation; reload it and try again.";
}
