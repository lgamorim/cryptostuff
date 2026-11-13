using CryptoStuff.Core;
using CryptoStuff.Core.Watchlists;
using CryptoStuff.Persistence.IntegrationTests.TestSupport;
using CryptoStuff.Persistence.Watchlists;

namespace CryptoStuff.Persistence.IntegrationTests.Watchlists;

// WatchlistService over the real repository and SQLite file, for the races
// only a real concurrency check can settle.
public sealed class WatchlistServiceTests : IAsyncLifetime
{
    // `!` because xUnit runs InitializeAsync, which assigns it, before every test.
    private TestDatabase _database = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _database = await TestDatabase.CreateMigratedAsync();

    public ValueTask DisposeAsync()
    {
        _database.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Should_ReturnConflictAndKeepCompetingCoin_When_AddCoinAsyncLosesRaceToAnotherAdd()
    {
        var watchlistId = await SaveNewAsync("Majors", "bitcoin");
        await using var context = _database.CreateContext();
        var service = new WatchlistService(
            new WatchlistRepository(context),
            new CompetingCoinLookup(cancellationToken => AddCoinThroughOwnServiceAsync(watchlistId, "dogecoin", cancellationToken)));

        var result = await service.AddCoinAsync(new WatchlistCoinCommand { WatchlistId = watchlistId, CoinId = "solana" }, Token);

        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().Be($"Watchlist {watchlistId} was changed by another operation; reload it and try again.");
        (await FindFreshAsync(watchlistId)).CoinIds.Should().Equal("bitcoin", "dogecoin");
    }

    // The competing writer: a second service over its own context, as a
    // second request or CLI invocation would be.
    private async Task AddCoinThroughOwnServiceAsync(int watchlistId, string coinId, CancellationToken cancellationToken)
    {
        await using var context = _database.CreateContext();
        var service = new WatchlistService(new WatchlistRepository(context), new CompetingCoinLookup(_ => Task.CompletedTask));
        var result = await service.AddCoinAsync(new WatchlistCoinCommand { WatchlistId = watchlistId, CoinId = coinId }, cancellationToken);
        result.IsSuccess.Should().BeTrue("the competing add commits first");
    }

    private async Task<int> SaveNewAsync(string name, params string[] coinIds)
    {
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);
        var watchlist = Watchlist.Create(name);
        foreach (var coinId in coinIds)
        {
            watchlist.AddCoin(coinId);
        }

        repository.Add(watchlist);
        await repository.SaveChangesAsync(Token);
        return watchlist.Id;
    }

    private async Task<Watchlist> FindFreshAsync(int id)
    {
        await using var context = _database.CreateContext();
        return await new WatchlistRepository(context).FindAsync(id, Token)
            ?? throw new InvalidOperationException($"Watchlist {id} was expected in the database but is missing.");
    }
}
