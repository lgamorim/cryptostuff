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
    [InlineData(CostBasisMethod.AverageCost, "average-cost")]
    [InlineData(CostBasisMethod.Fifo, "fifo")]
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
        var amount = decimal.Parse(literal, CultureInfo.InvariantCulture);
        var saved = await SaveNewAsync(
            "Long term",
            "usd",
            CostBasisMethod.AverageCost,
            new TransactionDetails { Kind = TransactionKind.Buy, CoinId = "bitcoin", Quantity = amount, UnitPrice = amount, Fee = amount, OccurredAt = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero) });

        var found = await FindFreshAsync(saved.Id);

        var transaction = found.Transactions.Should().ContainSingle().Subject;
        transaction.Quantity.Should().Be(amount);
        transaction.UnitPrice.Should().Be(amount);
        transaction.Fee.Should().Be(amount);
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
            .WithInnerException<DbUpdateConcurrencyException>();
        (await FindFreshAsync(saved.Id)).Transactions.Should().ContainSingle().Which.CoinId.Should().Be("bitcoin");
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
        await ExecuteAsync("UPDATE Portfolios SET CostBasisMethod = 'lifo'");
        await using var context = _database.CreateContext();
        var repository = new PortfolioRepository(context);

        var act = () => repository.FindAsync(saved.Id, Token);

        (await act.Should().ThrowAsync<InvalidDataException>()).WithMessage("*'lifo'*cost-basis method*");
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
