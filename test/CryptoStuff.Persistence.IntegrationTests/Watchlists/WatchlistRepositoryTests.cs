using CryptoStuff.Core;
using CryptoStuff.Core.Watchlists;
using CryptoStuff.Persistence.IntegrationTests.TestSupport;
using CryptoStuff.Persistence.Watchlists;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CryptoStuff.Persistence.IntegrationTests.Watchlists;

public sealed class WatchlistRepositoryTests : IAsyncLifetime
{
    // SQLite extended result codes: https://www.sqlite.org/rescode.html
    private const int UniqueViolation = 2067;       // SQLITE_CONSTRAINT_UNIQUE
    private const int PrimaryKeyViolation = 1555;   // SQLITE_CONSTRAINT_PRIMARYKEY
    private const int ForeignKeyViolation = 787;    // SQLITE_CONSTRAINT_FOREIGNKEY

    // `!` because xUnit runs InitializeAsync, which assigns it, before every test.
    private TestDatabase _database = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _database = await TestDatabase.CreateMigratedAsync();

    public ValueTask DisposeAsync()
    {
        _database.Dispose();
        return ValueTask.CompletedTask;
    }

    // Add and round trip.

    [Fact]
    public async Task Should_AssignIncreasingIds_When_WatchlistsSaved()
    {
        var first = await SaveNewAsync("Majors");
        var second = await SaveNewAsync("Memes");

        first.Id.Should().BePositive();
        second.Id.Should().BeGreaterThan(first.Id);
    }

    [Fact]
    public async Task Should_KeepNameAndCoinOrder_When_WatchlistRoundTrips()
    {
        var saved = await SaveNewAsync("Majors", "solana", "bitcoin", "ethereum");

        var found = await FindFreshAsync(saved.Id);

        found.Name.Should().Be("Majors");
        found.CoinIds.Should().Equal("solana", "bitcoin", "ethereum");
    }

    [Fact]
    public async Task Should_RoundTripEmptyCoinList_When_WatchlistHasNoCoins()
    {
        var saved = await SaveNewAsync("Empty");

        var found = await FindFreshAsync(saved.Id);

        found.CoinIds.Should().BeEmpty();
    }

    // Find, list, and name lookup.

    [Fact]
    public async Task Should_ReturnNull_When_FindingUnknownId()
    {
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);

        var found = await repository.FindAsync(42, Token);

        found.Should().BeNull();
    }

    [Fact]
    public async Task Should_ListEveryWatchlistOrderedById_When_Listing()
    {
        var first = await SaveNewAsync("Zeta", "bitcoin");
        var second = await SaveNewAsync("Alpha", "ethereum");
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);

        var listed = await repository.ListAsync(Token);

        listed.Select(w => w.Id).Should().Equal(first.Id, second.Id);
        listed[0].CoinIds.Should().Equal("bitcoin");
        listed[1].CoinIds.Should().Equal("ethereum");
    }

    [Fact]
    public async Task Should_ReturnEmptyList_When_NoWatchlistsSaved()
    {
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);

        var listed = await repository.ListAsync(Token);

        listed.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Majors")]
    [InlineData("MAJORS")]
    [InlineData("majors")]
    public async Task Should_ReportNameExists_When_SavedNameMatchesIgnoringCase(string name)
    {
        await SaveNewAsync("Majors");
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);

        var exists = await repository.NameExistsAsync(name, excludingId: null, Token);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReportNameAbsent_When_NoSavedNameMatches()
    {
        await SaveNewAsync("Majors");
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);

        var exists = await repository.NameExistsAsync("Memes", excludingId: null, Token);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Should_IgnoreExcludedWatchlist_When_CheckingNameExists()
    {
        var saved = await SaveNewAsync("Majors");
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);

        var exists = await repository.NameExistsAsync("MAJORS", saved.Id, Token);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReportNameExists_When_AnotherWatchlistThanTheExcludedOneHasIt()
    {
        await SaveNewAsync("Majors");
        var other = await SaveNewAsync("Memes");
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);

        var exists = await repository.NameExistsAsync("majors", other.Id, Token);

        exists.Should().BeTrue();
    }

    // Updates and deletes.

    [Fact]
    public async Task Should_PersistRename_When_Saved()
    {
        var saved = await SaveNewAsync("Majors");
        await using (var context = _database.CreateContext())
        {
            var repository = new WatchlistRepository(context);
            var watchlist = await FindSavedAsync(repository, saved.Id);
            watchlist.Rename("Large caps");
            await repository.SaveChangesAsync(Token);
        }

        var found = await FindFreshAsync(saved.Id);

        found.Name.Should().Be("Large caps");
    }

    [Fact]
    public async Task Should_PersistCoinChangesInOrder_When_Saved()
    {
        var saved = await SaveNewAsync("Majors", "bitcoin", "ethereum", "solana");
        await using (var context = _database.CreateContext())
        {
            var repository = new WatchlistRepository(context);
            var watchlist = await FindSavedAsync(repository, saved.Id);
            watchlist.RemoveCoin("ethereum");
            watchlist.AddCoin("cardano");
            await repository.SaveChangesAsync(Token);
        }

        var found = await FindFreshAsync(saved.Id);

        found.CoinIds.Should().Equal("bitcoin", "solana", "cardano");
    }

    [Fact]
    public async Task Should_PersistDelete_When_Saved()
    {
        var saved = await SaveNewAsync("Majors", "bitcoin");
        var kept = await SaveNewAsync("Memes");
        await using (var context = _database.CreateContext())
        {
            var repository = new WatchlistRepository(context);
            var watchlist = await FindSavedAsync(repository, saved.Id);
            repository.Remove(watchlist);
            await repository.SaveChangesAsync(Token);
        }

        await using var verification = _database.CreateContext();
        var remaining = await new WatchlistRepository(verification).ListAsync(Token);

        remaining.Select(w => w.Id).Should().Equal(kept.Id);
    }

    [Fact]
    public async Task Should_NotReuseId_When_HighestIdDeleted()
    {
        await SaveNewAsync("Majors");
        var deleted = await SaveNewAsync("Memes");
        await using (var context = _database.CreateContext())
        {
            var repository = new WatchlistRepository(context);
            repository.Remove(await FindSavedAsync(repository, deleted.Id));
            await repository.SaveChangesAsync(Token);
        }

        var next = await SaveNewAsync("Stables");

        next.Id.Should().BeGreaterThan(deleted.Id);
    }

    // Conflict translation.

    [Theory]
    [InlineData("Majors")]
    [InlineData("MAJORS")]
    [InlineData("majors")]
    public async Task Should_ThrowPersistenceConflictException_When_AddedNameMatchesSavedNameIgnoringCase(string name)
    {
        await SaveNewAsync("Majors");
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);
        repository.Add(Watchlist.Create(name));

        var act = () => repository.SaveChangesAsync(Token);

        (await act.Should().ThrowAsync<PersistenceConflictException>())
            .WithInnerException<DbUpdateException>()
            .WithInnerException<SqliteException>()
            .Which.SqliteExtendedErrorCode.Should().Be(UniqueViolation);
    }

    [Fact]
    public async Task Should_ThrowPersistenceConflictException_When_RenamedToSavedNameIgnoringCase()
    {
        await SaveNewAsync("Majors");
        var renamed = await SaveNewAsync("Memes");
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);
        var watchlist = await FindSavedAsync(repository, renamed.Id);
        watchlist.Rename("mAjOrS");

        var act = () => repository.SaveChangesAsync(Token);

        await act.Should().ThrowAsync<PersistenceConflictException>();
    }

    [Fact]
    public async Task Should_KeepFirstWatchlist_When_DuplicateNameSaveFails()
    {
        var saved = await SaveNewAsync("Majors", "bitcoin");
        await using (var context = _database.CreateContext())
        {
            var repository = new WatchlistRepository(context);
            repository.Add(Watchlist.Create("MAJORS"));
            await FluentActions.Awaiting(() => repository.SaveChangesAsync(Token))
                .Should().ThrowAsync<PersistenceConflictException>();
        }

        await using var verification = _database.CreateContext();
        var listed = await new WatchlistRepository(verification).ListAsync(Token);

        listed.Should().ContainSingle().Which.Id.Should().Be(saved.Id);
    }

    [Fact]
    public async Task Should_ThrowPersistenceConflictException_When_PrimaryKeyAlreadyTaken()
    {
        var saved = await SaveNewAsync("Majors");
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);
        var duplicate = Watchlist.Create("Memes");
        repository.Add(duplicate);
        // An explicit id on insert is the only way to reach the primary-key
        // constraint; the entity's own Id setter is private.
        context.Entry(duplicate).Property(w => w.Id).CurrentValue = saved.Id;

        var act = () => repository.SaveChangesAsync(Token);

        (await act.Should().ThrowAsync<PersistenceConflictException>())
            .WithInnerException<DbUpdateException>()
            .WithInnerException<SqliteException>()
            .Which.SqliteExtendedErrorCode.Should().Be(PrimaryKeyViolation);
    }

    [Fact]
    public async Task Should_ThrowPersistenceConflictException_When_DeletedByConcurrentWrite()
    {
        var saved = await SaveNewAsync("Majors");
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);
        var watchlist = await FindSavedAsync(repository, saved.Id);
        await DeleteFreshAsync(saved.Id);
        watchlist.Rename("Large caps");

        var act = () => repository.SaveChangesAsync(Token);

        (await act.Should().ThrowAsync<PersistenceConflictException>())
            .WithInnerException<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Should_ThrowPersistenceConflictException_When_RemovingWatchlistAlreadyDeletedConcurrently()
    {
        var saved = await SaveNewAsync("Majors");
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);
        var watchlist = await FindSavedAsync(repository, saved.Id);
        await DeleteFreshAsync(saved.Id);
        repository.Remove(watchlist);

        var act = () => repository.SaveChangesAsync(Token);

        await act.Should().ThrowAsync<PersistenceConflictException>();
    }

    [Fact]
    public async Task Should_ThrowPersistenceConflictExceptionAndKeepCompetingCoin_When_CoinAddedToWatchlistChangedSinceItWasRead()
    {
        var saved = await SaveNewAsync("Majors", "bitcoin");
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);
        var stale = await FindSavedAsync(repository, saved.Id);
        await AddCoinFreshAsync(saved.Id, "dogecoin");
        stale.AddCoin("solana");

        var act = () => repository.SaveChangesAsync(Token);

        (await act.Should().ThrowAsync<PersistenceConflictException>())
            .WithInnerException<DbUpdateConcurrencyException>();
        (await FindFreshAsync(saved.Id)).CoinIds.Should().Equal("bitcoin", "dogecoin");
    }

    [Fact]
    public async Task Should_ThrowPersistenceConflictException_When_WatchlistRenamedAfterCoinChangedSinceItWasRead()
    {
        var saved = await SaveNewAsync("Majors", "bitcoin");
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);
        var stale = await FindSavedAsync(repository, saved.Id);
        await AddCoinFreshAsync(saved.Id, "dogecoin");
        stale.Rename("Large caps");

        var act = () => repository.SaveChangesAsync(Token);

        (await act.Should().ThrowAsync<PersistenceConflictException>())
            .WithInnerException<DbUpdateConcurrencyException>();
        (await FindFreshAsync(saved.Id)).Name.Should().Be("Majors");
    }

    [Fact]
    public async Task Should_ThrowPersistenceConflictExceptionAndKeepWatchlist_When_RemovingWatchlistChangedSinceItWasRead()
    {
        var saved = await SaveNewAsync("Majors", "bitcoin");
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);
        var stale = await FindSavedAsync(repository, saved.Id);
        await AddCoinFreshAsync(saved.Id, "dogecoin");
        repository.Remove(stale);

        var act = () => repository.SaveChangesAsync(Token);

        (await act.Should().ThrowAsync<PersistenceConflictException>())
            .WithInnerException<DbUpdateConcurrencyException>();
        (await FindFreshAsync(saved.Id)).CoinIds.Should().Equal("bitcoin", "dogecoin");
    }

    [Fact]
    public async Task Should_StoreIncrementedVersion_When_ChangesSavedInSeparateUnitsOfWork()
    {
        var saved = await SaveNewAsync("Majors");
        await AddCoinFreshAsync(saved.Id, "bitcoin");
        await AddCoinFreshAsync(saved.Id, "dogecoin");

        var found = await FindFreshAsync(saved.Id);

        found.Version.Should().Be(3);
    }

    [Fact]
    public async Task Should_NotTranslateToPersistenceConflict_When_ForeignKeyViolated()
    {
        // No mapped table references watchlists yet, so the test database gains
        // a throwaway one to provoke a real foreign-key failure from the engine.
        var saved = await SaveNewAsync("Majors");
        await using (var arrange = _database.CreateContext())
        {
            await arrange.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE "Referrers" ("WatchlistId" INTEGER NOT NULL REFERENCES "Watchlists" ("Id"));
                INSERT INTO "Referrers" ("WatchlistId") VALUES ({0});
                """,
                [saved.Id],
                Token);
        }

        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);
        repository.Remove(await FindSavedAsync(repository, saved.Id));

        var act = () => repository.SaveChangesAsync(Token);

        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        thrown.Which.Should().NotBeOfType<DbUpdateConcurrencyException>();
        thrown.Which.InnerException.Should().BeOfType<SqliteException>()
            .Which.SqliteExtendedErrorCode.Should().Be(ForeignKeyViolation);
    }

    // Cancellation.

    [Fact]
    public async Task Should_ThrowOperationCanceledException_When_SaveCanceled()
    {
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);
        repository.Add(Watchlist.Create("Majors"));

        var act = () => repository.SaveChangesAsync(new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ThrowOperationCanceledException_When_ListCanceled()
    {
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);

        var act = () => repository.ListAsync(new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ThrowOperationCanceledException_When_FindCanceled()
    {
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);

        var act = () => repository.FindAsync(1, new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ThrowOperationCanceledException_When_NameLookupCanceled()
    {
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);

        var act = () => repository.NameExistsAsync("Majors", excludingId: null, new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // Helpers: each runs in its own context, so the arrange and verify steps
    // never share a change tracker with the step under test.

    private async Task<Watchlist> SaveNewAsync(string name, params string[] coinIds)
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
        return watchlist;
    }

    private async Task<Watchlist> FindFreshAsync(int id)
    {
        await using var context = _database.CreateContext();
        return await FindSavedAsync(new WatchlistRepository(context), id);
    }

    private async Task AddCoinFreshAsync(int id, string coinId)
    {
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);
        (await FindSavedAsync(repository, id)).AddCoin(coinId).Should().BeTrue();
        await repository.SaveChangesAsync(Token);
    }

    private static async Task<Watchlist> FindSavedAsync(WatchlistRepository repository, int id) =>
        await repository.FindAsync(id, Token)
            ?? throw new InvalidOperationException($"Watchlist {id} was expected in the database but is missing.");

    private async Task DeleteFreshAsync(int id)
    {
        await using var context = _database.CreateContext();
        var repository = new WatchlistRepository(context);
        repository.Remove(await FindSavedAsync(repository, id));
        await repository.SaveChangesAsync(Token);
    }
}
