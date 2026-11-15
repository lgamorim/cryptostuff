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
        if (await repository.NameExistsAsync(watchlist.Name, excludingId: null, cancellationToken))
        {
            return NameTaken(watchlist.Name);
        }

        repository.Add(watchlist);
        return await SaveAsync(watchlist, cancellationToken) == SaveOutcome.Saved ? Success(watchlist) : NameTaken(watchlist.Name);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<WatchlistView>> RenameAsync(RenameWatchlistCommand command, CancellationToken cancellationToken)
    {
        var watchlist = await repository.FindAsync(command.WatchlistId, cancellationToken);
        if (watchlist is null)
        {
            return NotFound<WatchlistView>(command.WatchlistId);
        }

        var name = NameValidator.Normalize(command.Name);
        if (await repository.NameExistsAsync(name, command.WatchlistId, cancellationToken))
        {
            return NameTaken(name);
        }

        watchlist.Rename(name);

        // Any race can lose this save: another watchlist taking the name since
        // the check above, another write moving the version on, or another
        // writer deleting the watchlist.
        return await SaveAsync(watchlist, cancellationToken) switch
        {
            SaveOutcome.Saved => Success(watchlist),
            SaveOutcome.Deleted => NotFound<WatchlistView>(watchlist.Id),
            _ => ServiceResult<WatchlistView>.Failure(
                ServiceErrorCode.Conflict,
                $"Watchlist {watchlist.Id} could not be saved: another watchlist may have taken the name '{name}', or another operation changed it; reload it and try again."),
        };
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
        return await SaveAsync(watchlist, cancellationToken) switch
        {
            SaveOutcome.Saved => ServiceResult.Success(),
            SaveOutcome.Deleted => ServiceResult.Failure(ServiceErrorCode.NotFound, NotFoundDetail(id)),
            _ => ServiceResult.Failure(ServiceErrorCode.Conflict, ChangedConcurrentlyDetail(id)),
        };
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
        return await cryptocurrencyService.GetKnownCoinPricesAsync(priceQuery, cancellationToken);
    }

    private async Task<ServiceResult<WatchlistView>> SaveCoinChangeAsync(Watchlist watchlist, CancellationToken cancellationToken) =>
        await SaveAsync(watchlist, cancellationToken) switch
        {
            SaveOutcome.Saved => Success(watchlist),
            SaveOutcome.Deleted => NotFound<WatchlistView>(watchlist.Id),
            _ => ServiceResult<WatchlistView>.Failure(ServiceErrorCode.Conflict, ChangedConcurrentlyDetail(watchlist.Id)),
        };

    /// <summary>
    /// Saves the pending change to <paramref name="watchlist"/>. On a
    /// <see cref="PersistenceConflictException"/> it discards that change, so
    /// nothing stale stays pending, and tells a watchlist another writer deleted
    /// from any other conflict. Any other exception, including cancellation and
    /// <see cref="PersistenceBusyException"/>, propagates.
    /// </summary>
    private async Task<SaveOutcome> SaveAsync(Watchlist watchlist, CancellationToken cancellationToken)
    {
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
            return SaveOutcome.Saved;
        }
        catch (PersistenceConflictException)
        {
            repository.Discard(watchlist);

            // An id of zero is a watchlist never saved, which no one can have deleted.
            return watchlist.Id != 0 && !await repository.ExistsAsync(watchlist.Id, cancellationToken)
                ? SaveOutcome.Deleted
                : SaveOutcome.Conflicted;
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
