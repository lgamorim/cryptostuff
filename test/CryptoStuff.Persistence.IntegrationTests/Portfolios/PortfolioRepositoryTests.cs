using System.Globalization;
using CryptoStuff.Core;
using CryptoStuff.Core.Portfolios;
using CryptoStuff.Persistence.IntegrationTests.TestSupport;
using CryptoStuff.Persistence.Portfolios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CryptoStuff.Persistence.IntegrationTests.Portfolios;

public sealed class PortfolioRepositoryTests : IAsyncLifetime
{
    // SQLite extended result codes: https://www.sqlite.org/rescode.html
    private const int UniqueViolation = 2067;       // SQLITE_CONSTRAINT_UNIQUE
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

    [Fact]
    public async Task Should_AssignIncreasingIds_When_PortfoliosSaved()
    {
        var first = await SaveNewAsync("Long term");
        var second = await SaveNewAsync("Trading");

        first.Id.Should().BePositive();
        second.Id.Should().BeGreaterThan(first.Id);
    }

    [Fact]
    public async Task Should_KeepEveryField_When_PortfolioWithTransactionsRoundTrips()
    {
        var buyTime = new DateTimeOffset(2026, 1, 5, 12, 30, 15, 123, TimeSpan.Zero).AddTicks(4567);
        var sellTime = new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.FromHours(2));
        var saved = await SaveNewAsync(
            "Long term",
            "eur",
            CostBasisMethod.Fifo,
            new TransactionDetails { Kind = TransactionKind.Buy, CoinId = "Bitcoin", Quantity = 2.5m, UnitPrice = 40000.25m, Fee = 12.5m, OccurredAt = buyTime },
            new TransactionDetails { Kind = TransactionKind.Sell, CoinId = "bitcoin", Quantity = 1m, UnitPrice = 50000m, OccurredAt = sellTime });

        var found = await FindFreshAsync(saved.Id);

        found.Name.Should().Be("Long term");
        found.VsCurrency.Should().Be("eur");
        found.CostBasisMethod.Should().Be(CostBasisMethod.Fifo);
        found.Version.Should().Be(saved.Version);
        found.Transactions.Should().HaveCount(2);
        var buy = found.Transactions[0];
        buy.Id.Should().Be(saved.Transactions[0].Id).And.BePositive();
        buy.Sequence.Should().Be(1);
        buy.Kind.Should().Be(TransactionKind.Buy);
        buy.CoinId.Should().Be("bitcoin");
        buy.Quantity.Should().Be(2.5m);
        buy.UnitPrice.Should().Be(40000.25m);
        buy.Fee.Should().Be(12.5m);
        buy.OccurredAt.Should().Be(buyTime);
        buy.OccurredAt.Offset.Should().Be(TimeSpan.Zero);
        var sell = found.Transactions[1];
        sell.Id.Should().Be(saved.Transactions[1].Id).And.NotBe(buy.Id);
        sell.Sequence.Should().Be(2);
        sell.Kind.Should().Be(TransactionKind.Sell);
        sell.Fee.Should().Be(0m);
        sell.OccurredAt.Should().Be(sellTime);
        sell.OccurredAt.Offset.Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(CostBasisMethod.Lifo)]
    [InlineData(CostBasisMethod.Hifo)]
    public async Task Should_KeepTheLotMethod_When_PortfolioRoundTrips(CostBasisMethod method)
    {
        var saved = await SaveNewAsync("Long term", "usd", method, Buy("bitcoin"));

        var found = await FindFreshAsync(saved.Id);

        found.CostBasisMethod.Should().Be(method);
        found.Transactions.Should().ContainSingle();
    }

    [Theory]
    [InlineData(CostBasisMethod.AverageCost, "average-cost")]
    [InlineData(CostBasisMethod.Fifo, "fifo")]
    [InlineData(CostBasisMethod.Lifo, "lifo")]
    [InlineData(CostBasisMethod.Hifo, "hifo")]
    public async Task Should_StoreCostBasisMethodAsToken_When_PortfolioSaved(CostBasisMethod method, string token)
    {
        await SaveNewAsync("Long term", "usd", method);

        var stored = await ReadColumnAsync("SELECT CAST(CostBasisMethod AS TEXT) AS Value FROM Portfolios");

        stored.Should().Equal(token);
    }

    [Theory]
    [InlineData(TransactionKind.Buy, "buy")]
    [InlineData(TransactionKind.Sell, "sell")]
    public async Task Should_StoreTransactionKindAsToken_When_PortfolioSaved(TransactionKind kind, string token)
    {
        var buyTime = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var details = new List<TransactionDetails>
        {
            new() { Kind = TransactionKind.Buy, CoinId = "bitcoin", Quantity = 1m, UnitPrice = 1m, OccurredAt = buyTime },
        };
        if (kind == TransactionKind.Sell)
        {
            details.Add(new() { Kind = TransactionKind.Sell, CoinId = "bitcoin", Quantity = 1m, UnitPrice = 1m, OccurredAt = buyTime.AddDays(1) });
        }

        await SaveNewAsync("Long term", "usd", CostBasisMethod.AverageCost, [.. details]);

        var stored = await ReadColumnAsync("SELECT CAST(Kind AS TEXT) AS Value FROM PortfolioTransactions ORDER BY Id DESC LIMIT 1");

        stored.Should().Equal(token);
    }

    [Theory]
    [InlineData("0.00000001")]
    [InlineData("0.1")]
    [InlineData("12345678901234567890.123456789")]
    [InlineData("79228162514264337593543950335")]
    public async Task Should_RoundTripDecimalsExactly_When_PortfolioSaved(string literal)
    {
        // Each column gets the amount on a coin of its own: one buy carrying the
        // largest decimal as both quantity and price would cost more than a
        // decimal holds, which the portfolio refuses.
        var amount = decimal.Parse(literal, CultureInfo.InvariantCulture);
        var occurredAt = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var saved = await SaveNewAsync(
            "Long term",
            "usd",
            CostBasisMethod.AverageCost,
            new TransactionDetails { Kind = TransactionKind.Buy, CoinId = "quantity", Quantity = amount, UnitPrice = 0m, OccurredAt = occurredAt },
            new TransactionDetails { Kind = TransactionKind.Buy, CoinId = "unit-price", Quantity = 1m, UnitPrice = amount, OccurredAt = occurredAt },
            new TransactionDetails { Kind = TransactionKind.Buy, CoinId = "fee", Quantity = 1m, UnitPrice = 0m, Fee = amount, OccurredAt = occurredAt });

        var found = await FindFreshAsync(saved.Id);

        found.Transactions.Single(t => t.CoinId == "quantity").Quantity.Should().Be(amount);
        found.Transactions.Single(t => t.CoinId == "unit-price").UnitPrice.Should().Be(amount);
        found.Transactions.Single(t => t.CoinId == "fee").Fee.Should().Be(amount);
    }

    [Fact]
    public async Task Should_StoreDecimalsAsText_When_PortfolioSaved()
    {
        await SaveNewAsync(
            "Long term",
            "usd",
            CostBasisMethod.AverageCost,
            new TransactionDetails { Kind = TransactionKind.Buy, CoinId = "bitcoin", Quantity = 0.00000001m, UnitPrice = 1m, OccurredAt = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero) });

        var stored = await ReadColumnAsync("SELECT typeof(Quantity) || typeof(UnitPrice) || typeof(Fee) AS Value FROM PortfolioTransactions");

        stored.Should().Equal("texttexttext");
    }

    [Fact]
    public async Task Should_StoreOccurredAtAsUtc_When_TransactionHappenedAtAnotherOffset()
    {
        await SaveNewAsync(
            "Long term",
            "usd",
            CostBasisMethod.AverageCost,
            new TransactionDetails { Kind = TransactionKind.Buy, CoinId = "bitcoin", Quantity = 1m, UnitPrice = 1m, OccurredAt = new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.FromHours(2)) });

        var stored = await ReadColumnAsync("SELECT OccurredAt AS Value FROM PortfolioTransactions");

        stored.Should().Equal("2026-02-01 07:00:00");
    }

    [Theory]
    [InlineData("Long term")]
    [InlineData("LONG TERM")]
    [InlineData("long term")]
    public async Task Should_ThrowPersistenceConflictException_When_AddedNameMatchesSavedNameIgnoringCase(string name)
    {
        await SaveNewAsync("Long term");
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);
        repository.Add(Portfolio.Create(name, "usd", CostBasisMethod.AverageCost));

        var act = () => repository.SaveChangesAsync(Token);

        (await act.Should().ThrowAsync<PersistenceConflictException>())
            .WithInnerException<DbUpdateException>()
            .WithInnerException<SqliteException>()
            .Which.SqliteExtendedErrorCode.Should().Be(UniqueViolation);
    }

    [Theory]
    [InlineData("AÇÃO")]
    [InlineData("AçÃo")]
    public async Task Should_ThrowPersistenceConflictException_When_AddedNameMatchesSavedNameIgnoringNonAsciiCase(string name)
    {
        await SaveNewAsync("Ação");
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);
        repository.Add(Portfolio.Create(name, "usd", CostBasisMethod.AverageCost));

        var act = () => repository.SaveChangesAsync(Token);

        (await act.Should().ThrowAsync<PersistenceConflictException>())
            .WithInnerException<DbUpdateException>()
            .WithInnerException<SqliteException>()
            .Which.SqliteExtendedErrorCode.Should().Be(UniqueViolation);
    }

    [Fact]
    public async Task Should_ThrowPersistenceConflictException_When_PortfolioChangedSinceItWasRead()
    {
        var saved = await SaveNewAsync("Long term");
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);
        var stale = await FindSavedAsync(repository, saved.Id);
        await RenameFreshAsync(saved.Id, "Renamed first");
        stale.Update("Renamed second", CostBasisMethod.Fifo);

        var act = () => repository.SaveChangesAsync(Token);

        (await act.Should().ThrowAsync<PersistenceConflictException>())
            .WithInnerException<DbUpdateConcurrencyException>();
        (await FindFreshAsync(saved.Id)).Name.Should().Be("Renamed first");
    }

    // Both adds take sequence 1, and EF inserts the new transaction before it
    // checks the portfolio's version, so the unique (PortfolioId, Sequence)
    // index catches the lost race first; it is still the same conflict.
    [Fact]
    public async Task Should_ThrowPersistenceConflictException_When_TransactionAddedToPortfolioChangedSinceItWasRead()
    {
        var saved = await SaveNewAsync("Long term");
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);
        var stale = await FindSavedAsync(repository, saved.Id);
        await AddBuyFreshAsync(saved.Id);
        stale.AddTransaction(Buy("ethereum")).IsValid.Should().BeTrue();

        var act = () => repository.SaveChangesAsync(Token);

        (await act.Should().ThrowAsync<PersistenceConflictException>())
            .WithInnerException<DbUpdateException>()
            .WithInnerException<SqliteException>()
            .Which.SqliteExtendedErrorCode.Should().Be(UniqueViolation);
        (await FindFreshAsync(saved.Id)).Transactions.Should().ContainSingle().Which.CoinId.Should().Be("bitcoin");
    }

    // Here the stale add's sequence collides with nothing, so the version
    // check is what finds the portfolio changed, and the inserted row goes
    // back with the rest of the save.
    [Fact]
    public async Task Should_ThrowPersistenceConflictExceptionAndStoreNoTransaction_When_TransactionAddedToPortfolioRenamedSinceItWasRead()
    {
        var saved = await SaveNewAsync("Long term");
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);
        var stale = await FindSavedAsync(repository, saved.Id);
        await RenameFreshAsync(saved.Id, "Renamed");
        stale.AddTransaction(Buy("ethereum")).IsValid.Should().BeTrue();

        var act = () => repository.SaveChangesAsync(Token);

        (await act.Should().ThrowAsync<PersistenceConflictException>())
            .WithInnerException<DbUpdateConcurrencyException>();
        var found = await FindFreshAsync(saved.Id);
        found.Name.Should().Be("Renamed");
        found.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_NotReuseTheRemovedTransactionsSequence_When_AddedInALaterUnitOfWork()
    {
        var saved = await SaveNewAsync("Long term", "usd", CostBasisMethod.AverageCost, Buy("bitcoin"), Buy("ethereum"));
        await using (var context = _database.CreateContext())
        {
            var repository = new PortfolioRepository(context);
            var portfolio = await FindSavedAsync(repository, saved.Id);
            portfolio.RemoveTransaction(portfolio.Transactions.Single(t => t.Sequence == 2)).IsValid.Should().BeTrue();
            await repository.SaveChangesAsync(Token);
        }

        await AddBuyFreshAsync(saved.Id);

        (await FindFreshAsync(saved.Id)).Transactions.Select(t => t.Sequence).Should().Equal(1, 3);
    }

    // A writer that does not keep the stored mark, such as a release from
    // before it existed opening an upgraded file, numbers by the highest
    // sequence held and leaves the mark behind. The next add must still get
    // a free number rather than collide with that row on every attempt.
    [Fact]
    public async Task Should_NumberAfterTheHighestHeldSequence_When_ATransactionWasStoredPastTheMark()
    {
        var saved = await SaveNewAsync("Long term", "usd", CostBasisMethod.AverageCost, Buy("bitcoin"));
        await ExecuteAsync($"""
            INSERT INTO "PortfolioTransactions" ("Sequence", "Kind", "CoinId", "Quantity", "UnitPrice", "Fee", "OccurredAt", "PortfolioId")
            VALUES (2, 'buy', 'ethereum', '1', '1', '0', '2026-01-05 00:00:00', {saved.Id})
            """);

        await AddBuyFreshAsync(saved.Id);

        (await FindFreshAsync(saved.Id)).Transactions.Select(t => t.Sequence).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Should_RejectTheRow_When_ASecondTransactionInThePortfolioHasTheSameSequence()
    {
        var saved = await SaveNewAsync("Long term", "usd", CostBasisMethod.AverageCost, Buy("bitcoin"));

        var act = () => ExecuteAsync($"""
            INSERT INTO "PortfolioTransactions" ("Sequence", "Kind", "CoinId", "Quantity", "UnitPrice", "Fee", "OccurredAt", "PortfolioId")
            VALUES (1, 'buy', 'ethereum', '1', '1', '0', '2026-01-05 00:00:00', {saved.Id})
            """);

        (await act.Should().ThrowAsync<SqliteException>()).Which.SqliteExtendedErrorCode.Should().Be(UniqueViolation);
    }

    [Fact]
    public async Task Should_NumberEachPortfolioFromOne_When_TwoPortfoliosRecordTransactions()
    {
        var first = await SaveNewAsync("Long term", "usd", CostBasisMethod.AverageCost, Buy("bitcoin"));
        var second = await SaveNewAsync("Trading", "usd", CostBasisMethod.AverageCost, Buy("ethereum"));

        (await FindFreshAsync(first.Id)).Transactions.Should().ContainSingle().Which.Sequence.Should().Be(1);
        (await FindFreshAsync(second.Id)).Transactions.Should().ContainSingle().Which.Sequence.Should().Be(1);
    }

    [Fact]
    public async Task Should_StoreIncrementedVersion_When_ChangesSavedInSeparateUnitsOfWork()
    {
        var saved = await SaveNewAsync("Long term");
        await RenameFreshAsync(saved.Id, "Renamed");
        await AddBuyFreshAsync(saved.Id);

        var found = await FindFreshAsync(saved.Id);

        found.Version.Should().Be(3);
    }

    [Fact]
    public async Task Should_DeleteItsTransactionsOnly_When_PortfolioDeleted()
    {
        var deleted = await SaveNewAsync("Long term", "usd", CostBasisMethod.AverageCost, Buy("bitcoin"), Buy("ethereum"));
        var kept = await SaveNewAsync("Trading", "usd", CostBasisMethod.AverageCost, Buy("solana"));
        await using (var context = _database.CreateContext())
        {
            var repository = new PortfolioRepository(context);
            repository.Remove(await FindSavedAsync(repository, deleted.Id));
            await repository.SaveChangesAsync(Token);
        }

        (await ReadColumnAsync("SELECT CAST(PortfolioId AS TEXT) AS Value FROM PortfolioTransactions"))
            .Should().Equal(kept.Id.ToString(CultureInfo.InvariantCulture));
        (await FindFreshAsync(kept.Id)).Transactions.Should().ContainSingle().Which.CoinId.Should().Be("solana");
    }

    [Fact]
    public async Task Should_DeleteTransactionsInTheDatabase_When_PortfolioRowDeletedDirectly()
    {
        // Bypasses the change tracker, so only the foreign key's own cascade can remove the rows.
        var saved = await SaveNewAsync("Long term", "usd", CostBasisMethod.AverageCost, Buy("bitcoin"));
        await using (var context = _database.CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM Portfolios", Token);
        }

        (await ReadColumnAsync("SELECT CAST(Id AS TEXT) AS Value FROM PortfolioTransactions"))
            .Should().BeEmpty($"portfolio {saved.Id}'s transactions go with it");
    }

    [Fact]
    public async Task Should_NotReusePortfolioId_When_HighestIdDeleted()
    {
        await SaveNewAsync("Long term");
        var deleted = await SaveNewAsync("Trading");
        await DeleteFreshAsync(deleted.Id);

        var next = await SaveNewAsync("Stables");

        next.Id.Should().BeGreaterThan(deleted.Id);
    }

    [Fact]
    public async Task Should_NotReuseTransactionId_When_HighestIdDeleted()
    {
        var saved = await SaveNewAsync("Long term", "usd", CostBasisMethod.AverageCost, Buy("bitcoin"), Buy("ethereum"));
        var removedId = saved.Transactions[1].Id;
        await using (var context = _database.CreateContext())
        {
            var repository = new PortfolioRepository(context);
            var portfolio = await FindSavedAsync(repository, saved.Id);
            portfolio.RemoveTransaction(portfolio.Transactions.Single(t => t.Id == removedId)).IsValid.Should().BeTrue();
            await repository.SaveChangesAsync(Token);
        }

        await AddBuyFreshAsync(saved.Id);

        var found = await FindFreshAsync(saved.Id);
        found.Transactions.Should().HaveCount(2);
        found.Transactions.Select(t => t.Id).Should().NotContain(removedId);
    }

    [Fact]
    public async Task Should_ReturnNull_When_FindingUnknownId()
    {
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);

        var found = await repository.FindAsync(42, Token);

        found.Should().BeNull();
    }

    [Fact]
    public async Task Should_ListEveryPortfolioWithItsTransactionsOrderedById_When_Listing()
    {
        var first = await SaveNewAsync("Zeta", "usd", CostBasisMethod.AverageCost, Buy("bitcoin"), Buy("ethereum"));
        var second = await SaveNewAsync("Alpha", "usd", CostBasisMethod.Fifo, Buy("solana"));
        var third = await SaveNewAsync("Empty");
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);

        var listed = await repository.ListAsync(Token);

        listed.Select(p => p.Id).Should().Equal(first.Id, second.Id, third.Id);
        listed[0].Transactions.Select(t => t.CoinId).Should().Equal("bitcoin", "ethereum");
        listed[1].Transactions.Select(t => t.CoinId).Should().Equal("solana");
        listed[2].Transactions.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Long term")]
    [InlineData("LONG TERM")]
    [InlineData("long term")]
    public async Task Should_ReportNameExists_When_SavedNameMatchesIgnoringCase(string name)
    {
        await SaveNewAsync("Long term");
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);

        var exists = await repository.NameExistsAsync(name, excludingId: null, Token);

        exists.Should().BeTrue();
    }

    // SQLite's own NOCASE folds only ASCII letters.
    [Theory]
    [InlineData("AÇÃO")]
    [InlineData("AçÃo")]
    public async Task Should_ReportNameExists_When_SavedNameMatchesIgnoringNonAsciiCase(string name)
    {
        await SaveNewAsync("Ação");
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);

        var exists = await repository.NameExistsAsync(name, excludingId: null, Token);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReportNameAbsent_When_SavedNameDiffersOnlyInAccents()
    {
        await SaveNewAsync("Ação");
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);

        var exists = await repository.NameExistsAsync("Acao", excludingId: null, Token);

        exists.Should().BeFalse("case is ignored, but a letter with an accent is still a different letter");
    }

    [Fact]
    public async Task Should_ReportNameAbsent_When_NoSavedNameMatches()
    {
        await SaveNewAsync("Long term");
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);

        var exists = await repository.NameExistsAsync("Trading", excludingId: null, Token);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Should_IgnoreExcludedPortfolio_When_CheckingNameExists()
    {
        var saved = await SaveNewAsync("Long term");
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);

        var exists = await repository.NameExistsAsync("LONG TERM", saved.Id, Token);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReportNameExists_When_AnotherPortfolioThanTheExcludedOneHasIt()
    {
        await SaveNewAsync("Long term");
        var other = await SaveNewAsync("Trading");
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);

        var exists = await repository.NameExistsAsync("long term", other.Id, Token);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Should_PersistUpdate_When_Saved()
    {
        var saved = await SaveNewAsync("Long term");
        await using (var context = _database.CreateContext())
        {
            var repository = new PortfolioRepository(context);
            (await FindSavedAsync(repository, saved.Id)).Update("Large caps", CostBasisMethod.Fifo);
            await repository.SaveChangesAsync(Token);
        }

        var found = await FindFreshAsync(saved.Id);

        found.Name.Should().Be("Large caps");
        found.CostBasisMethod.Should().Be(CostBasisMethod.Fifo);
        found.Version.Should().Be(2);
    }

    [Fact]
    public async Task Should_PersistReplacedTransactionKeepingIdAndSequence_When_Saved()
    {
        var saved = await SaveNewAsync("Long term", "usd", CostBasisMethod.AverageCost, Buy("bitcoin"));
        var original = saved.Transactions.Single();
        await using (var context = _database.CreateContext())
        {
            var repository = new PortfolioRepository(context);
            var portfolio = await FindSavedAsync(repository, saved.Id);
            var replacement = Buy("ethereum") with { Quantity = 3.5m, UnitPrice = 2000.5m, Fee = 1.25m };
            portfolio.ReplaceTransaction(portfolio.Transactions.Single(), replacement).IsValid.Should().BeTrue();
            await repository.SaveChangesAsync(Token);
        }

        var found = await FindFreshAsync(saved.Id);

        var transaction = found.Transactions.Should().ContainSingle().Subject;
        transaction.Id.Should().Be(original.Id);
        transaction.Sequence.Should().Be(original.Sequence);
        transaction.CoinId.Should().Be("ethereum");
        transaction.Quantity.Should().Be(3.5m);
        transaction.UnitPrice.Should().Be(2000.5m);
        transaction.Fee.Should().Be(1.25m);
        found.Version.Should().Be(3);
    }

    [Fact]
    public async Task Should_PersistTransactionRemoval_When_Saved()
    {
        var saved = await SaveNewAsync("Long term", "usd", CostBasisMethod.AverageCost, Buy("bitcoin"), Buy("ethereum"));
        await using (var context = _database.CreateContext())
        {
            var repository = new PortfolioRepository(context);
            var portfolio = await FindSavedAsync(repository, saved.Id);
            portfolio.RemoveTransaction(portfolio.Transactions[0]).IsValid.Should().BeTrue();
            await repository.SaveChangesAsync(Token);
        }

        var found = await FindFreshAsync(saved.Id);

        found.Transactions.Should().ContainSingle().Which.CoinId.Should().Be("ethereum");
        (await ReadColumnAsync("SELECT CAST(Id AS TEXT) AS Value FROM PortfolioTransactions")).Should().HaveCount(1);
    }

    [Fact]
    public async Task Should_ThrowPersistenceConflictException_When_RemovingPortfolioChangedSinceItWasRead()
    {
        var saved = await SaveNewAsync("Long term");
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);
        var stale = await FindSavedAsync(repository, saved.Id);
        await RenameFreshAsync(saved.Id, "Renamed first");
        repository.Remove(stale);

        var act = () => repository.SaveChangesAsync(Token);

        (await act.Should().ThrowAsync<PersistenceConflictException>())
            .WithInnerException<DbUpdateConcurrencyException>();
        (await FindFreshAsync(saved.Id)).Name.Should().Be("Renamed first");
    }

    // EF inserts the new transaction before it can find the portfolio row gone, so
    // the engine's foreign key fails first; it is still the lost race it stands for.
    [Fact]
    public async Task Should_ThrowPersistenceConflictException_When_ChangingPortfolioDeletedConcurrently()
    {
        var saved = await SaveNewAsync("Long term");
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);
        var portfolio = await FindSavedAsync(repository, saved.Id);
        await DeleteFreshAsync(saved.Id);
        portfolio.AddTransaction(Buy("bitcoin")).IsValid.Should().BeTrue();

        var act = () => repository.SaveChangesAsync(Token);

        (await act.Should().ThrowAsync<PersistenceConflictException>())
            .WithInnerException<DbUpdateException>()
            .WithInnerException<SqliteException>()
            .Which.SqliteExtendedErrorCode.Should().Be(ForeignKeyViolation);
    }

    [Fact]
    public async Task Should_ImplementCoreRepositoryContract_When_Constructed()
    {
        await using var context = _database.CreateContext();

        var repository = new PortfolioRepository(context);

        repository.Should().BeAssignableTo<IPortfolioRepository>();
    }

    [Fact]
    public async Task Should_ThrowOperationCanceledException_When_SaveCanceled()
    {
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);
        repository.Add(Portfolio.Create("Long term", "usd", CostBasisMethod.AverageCost));

        var act = () => repository.SaveChangesAsync(new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ThrowOperationCanceledException_When_ExistenceCheckCanceled()
    {
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);

        var act = () => repository.ExistsAsync(1, new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ThrowOperationCanceledException_When_ListCanceled()
    {
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);

        var act = () => repository.ListAsync(new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ThrowOperationCanceledException_When_FindCanceled()
    {
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);

        var act = () => repository.FindAsync(1, new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ThrowOperationCanceledException_When_NameLookupCanceled()
    {
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);

        var act = () => repository.NameExistsAsync("Long term", excludingId: null, new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ThrowInvalidDataException_When_StoredCostBasisMethodIsNotAToken()
    {
        var saved = await SaveNewAsync("Long term");
        await ExecuteAsync("UPDATE Portfolios SET CostBasisMethod = 'specific-id'");
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);

        var act = () => repository.FindAsync(saved.Id, Token);

        (await act.Should().ThrowAsync<InvalidDataException>()).WithMessage("*'specific-id'*cost-basis method*");
    }

    [Fact]
    public async Task Should_ThrowInvalidDataException_When_StoredTransactionKindIsNotAToken()
    {
        var saved = await SaveNewAsync("Long term", "usd", CostBasisMethod.AverageCost, Buy("bitcoin"));
        await ExecuteAsync("UPDATE PortfolioTransactions SET Kind = 'transfer'");
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);

        var act = () => repository.FindAsync(saved.Id, Token);

        (await act.Should().ThrowAsync<InvalidDataException>()).WithMessage("*'transfer'*transaction kind*");
    }

    // Existence, read past the change tracker.

    [Fact]
    public async Task Should_ReportAbsent_When_NoPortfolioHasTheId()
    {
        await using var context = _database.CreateContext();

        var exists = await new PortfolioRepository(context).ExistsAsync(42, Token);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReportAbsent_When_PortfolioDeletedByAnotherContextAfterItWasRead()
    {
        var saved = await SaveNewAsync("Long term");
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);
        await FindSavedAsync(repository, saved.Id);
        await DeleteFreshAsync(saved.Id);

        var exists = await repository.ExistsAsync(saved.Id, Token);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReportExists_When_PortfolioSaved()
    {
        var saved = await SaveNewAsync("Long term");
        await using var context = _database.CreateContext();

        var exists = await new PortfolioRepository(context).ExistsAsync(saved.Id, Token);

        exists.Should().BeTrue();
    }

    // Discarding a stale change.

    [Fact]
    public async Task Should_DiscardTheStaleLedgerAndSaveNothingFurther_When_DiscardedAfterConflict()
    {
        var saved = await SaveNewAsync("Long term", "usd", CostBasisMethod.AverageCost, Buy("bitcoin"));
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);
        var portfolio = await FindSavedAsync(repository, saved.Id);
        await AddBuyFreshAsync(saved.Id);
        portfolio.AddTransaction(Buy("ethereum")).IsValid.Should().BeTrue();
        portfolio.RemoveTransaction(portfolio.Transactions[0]).IsValid.Should().BeTrue();
        var conflictingSave = () => repository.SaveChangesAsync(Token);
        await conflictingSave.Should().ThrowAsync<PersistenceConflictException>();

        repository.Discard(portfolio);
        await repository.SaveChangesAsync(Token);
        var reread = await FindSavedAsync(repository, saved.Id);

        // Read through the same context: none of the stale transactions is
        // still tracked to be reused in place of the stored rows.
        reread.Should().NotBeSameAs(portfolio);
        reread.Transactions.Select(t => t.CoinId).Should().Equal("bitcoin", "bitcoin");
        (await FindFreshAsync(saved.Id)).Transactions.Should().HaveCount(2);
    }

    [Fact]
    public async Task Should_DiscardTheUnsavedPortfolioWithItsTransactions_When_DiscardedAfterConflict()
    {
        await SaveNewAsync("Long term");
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);
        var duplicate = Portfolio.Create("Long term", "usd", CostBasisMethod.Fifo);
        duplicate.AddTransaction(Buy("bitcoin")).IsValid.Should().BeTrue();
        repository.Add(duplicate);
        var conflictingSave = () => repository.SaveChangesAsync(Token);
        await conflictingSave.Should().ThrowAsync<PersistenceConflictException>();

        repository.Discard(duplicate);

        // Nothing of it stays tracked, so the next save does not retry the insert.
        context.ChangeTracker.Entries().Should().BeEmpty();
        await repository.SaveChangesAsync(Token);
    }

    // The database busy with another writer.

    [Fact]
    public async Task Should_ThrowPersistenceBusyException_When_AnotherConnectionHoldsTheWriteLockOverALedgerChange()
    {
        var saved = await SaveNewAsync("Long term", "usd", CostBasisMethod.AverageCost, Buy("bitcoin"));
        await using var context = _database.CreateImpatientContext();
        var repository = new PortfolioRepository(context);
        (await FindSavedAsync(repository, saved.Id)).AddTransaction(Buy("bitcoin")).IsValid.Should().BeTrue();
        await using var writeLock = await WriteLock.HoldAsync(_database.ConnectionString, Token);

        var act = () => repository.SaveChangesAsync(Token);

        // SQLITE_BUSY: https://www.sqlite.org/rescode.html#busy
        (await act.Should().ThrowAsync<PersistenceBusyException>())
            .Which.GetBaseException().Should().BeOfType<SqliteException>()
            .Which.SqliteErrorCode.Should().Be(5);
    }

    // Helpers: each runs in its own context, so the arrange and verify steps
    // never share a change tracker with the step under test.

    private async Task<Portfolio> SaveNewAsync(string name) =>
        await SaveNewAsync(name, "usd", CostBasisMethod.AverageCost);

    private async Task<Portfolio> SaveNewAsync(string name, string vsCurrency, CostBasisMethod method, params TransactionDetails[] transactions)
    {
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);
        var portfolio = Portfolio.Create(name, vsCurrency, method);
        foreach (var details in transactions)
        {
            portfolio.AddTransaction(details).IsValid.Should().BeTrue("the arranged ledger must hold");
        }

        repository.Add(portfolio);
        await repository.SaveChangesAsync(Token);
        return portfolio;
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var context = _database.CreateContext();
        await context.Database.ExecuteSqlRawAsync(sql, Token);
    }

    // Reads a column's stored text straight from the file, bypassing the mapping.
    private async Task<List<string>> ReadColumnAsync(string sql)
    {
        await using var context = _database.CreateContext();
        return await context.Database.SqlQueryRaw<string>(sql).ToListAsync(Token);
    }

    private async Task<Portfolio> FindFreshAsync(int id)
    {
        await using var context = _database.CreateContext();
        return await FindSavedAsync(new PortfolioRepository(context), id);
    }

    private async Task RenameFreshAsync(int id, string name)
    {
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);
        (await FindSavedAsync(repository, id)).Update(name, CostBasisMethod.AverageCost);
        await repository.SaveChangesAsync(Token);
    }

    private async Task AddBuyFreshAsync(int id)
    {
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);
        (await FindSavedAsync(repository, id)).AddTransaction(Buy("bitcoin")).IsValid.Should().BeTrue();
        await repository.SaveChangesAsync(Token);
    }

    private static TransactionDetails Buy(string coinId) =>
        new() { Kind = TransactionKind.Buy, CoinId = coinId, Quantity = 1m, UnitPrice = 1m, OccurredAt = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero) };

    private async Task DeleteFreshAsync(int id)
    {
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);
        repository.Remove(await FindSavedAsync(repository, id));
        await repository.SaveChangesAsync(Token);
    }

    private static async Task<Portfolio> FindSavedAsync(PortfolioRepository repository, int id) =>
        await repository.FindAsync(id, Token)
            ?? throw new InvalidOperationException($"Portfolio {id} was expected in the database but is missing.");
}
