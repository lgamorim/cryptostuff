using CryptoStuff.Core.Alerts;
using CryptoStuff.Core.Portfolios;
using CryptoStuff.Core.Watchlists;
using CryptoStuff.Persistence.IntegrationTests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CryptoStuff.Persistence.IntegrationTests;

public class CryptoStuffDatabaseTests
{
    [Fact]
    public async Task Should_HaveNoPendingModelChanges_When_Migrated()
    {
        using var database = await TestDatabase.CreateMigratedAsync();
        await using var context = database.CreateContext();

        var hasPendingChanges = context.Database.HasPendingModelChanges();

        hasPendingChanges.Should().BeFalse("every model change needs a migration checked in beside it");
    }

    [Fact]
    public async Task Should_ApplyEveryMigration_When_Migrated()
    {
        using var database = await TestDatabase.CreateMigratedAsync();
        await using var context = database.CreateContext();

        var pending = await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);

        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_CreateMissingDirectory_When_MigratingFileDatabase()
    {
        var testRoot = Path.Combine(TestDatabase.Root, $"{Guid.NewGuid():N}");
        var filePath = Path.Combine(testRoot, "nested", "cryptostuff.db");
        try
        {
            await using (var context = TestDatabase.CreateContextFor(filePath))
            {
                await CryptoStuffDatabase.MigrateAsync(context, TestContext.Current.CancellationToken);
            }

            File.Exists(filePath).Should().BeTrue();
            await using var verification = TestDatabase.CreateContextFor(filePath);
            var count = await verification.Watchlists.CountAsync(TestContext.Current.CancellationToken);
            count.Should().Be(0);
        }
        finally
        {
            // Guarded so a migration that failed before creating the folder
            // reports its own exception, not DirectoryNotFoundException.
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Should_CreateTheFileInWalMode_When_MigratingANewFileDatabase()
    {
        using var database = await TestDatabase.CreateMigratedAsync();
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode";

        var journalMode = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        journalMode.Should().Be("wal", "readers must not wait on a writer, so only saves can meet a busy database");
    }

    [Fact]
    public async Task Should_NotCreateDirectory_When_NamedDatabaseIsInMemory()
    {
        var directory = Path.Combine(TestDatabase.Root, $"{Guid.NewGuid():N}");
        var connectionString = $"Data Source={Path.Combine(directory, "shared")};Mode=Memory;Cache=Shared";
        await using var context = CreateContext(connectionString);

        await CryptoStuffDatabase.MigrateAsync(context, TestContext.Current.CancellationToken);

        Directory.Exists(directory).Should().BeFalse();
    }

    [Theory]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=")]
    public async Task Should_Migrate_When_DatabaseHasNoFilePath(string connectionString)
    {
        await using var context = CreateContext(connectionString);

        var act = () => CryptoStuffDatabase.MigrateAsync(context, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Should_ThrowArgumentNullException_When_ContextIsNull()
    {
        // `!` passes null past the non-nullable parameter to exercise the guard.
        var act = () => CryptoStuffDatabase.MigrateAsync(null!, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public async Task Should_Migrate_When_ContextHasNoConnectionString()
    {
        // With no connection string, Microsoft.Data.Sqlite opens a private
        // temporary database, as it does for an empty data source.
        var options = new DbContextOptionsBuilder<CryptoStuffDbContext>().UseSqlite().Options;
        await using var context = new CryptoStuffDbContext(options);

        var act = () => CryptoStuffDatabase.MigrateAsync(context, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Should_KeepExistingData_When_MigratingAgain()
    {
        using var database = await TestDatabase.CreateMigratedAsync();
        await using (var context = database.CreateContext())
        {
            context.Watchlists.Add(Watchlist.Create("Majors"));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            await CryptoStuffDatabase.MigrateAsync(context, TestContext.Current.CancellationToken);
        }

        await using var verification = database.CreateContext();
        var names = await verification.Watchlists.Select(w => w.Name).ToListAsync(TestContext.Current.CancellationToken);
        names.Should().Equal("Majors");
    }

    [Fact]
    public async Task Should_KeepWatchlistData_When_UpgradingFromInitialCreateToAddPortfolios()
    {
        var filePath = Path.Combine(TestDatabase.Root, $"{Guid.NewGuid():N}.db");
        try
        {
            await using (var before = TestDatabase.CreateContextFor(filePath))
            {
                // Stops at the schema the first release shipped, which
                // CryptoStuffDatabase offers no way to do.
                Directory.CreateDirectory(TestDatabase.Root);
                await before.GetService<IMigrator>().MigrateAsync("InitialCreate", TestContext.Current.CancellationToken);
                await before.Database.ExecuteSqlRawAsync(
                    """INSERT INTO "Watchlists" ("Name", "CoinIds") VALUES ('Majors', '["bitcoin","ethereum"]')""",
                    TestContext.Current.CancellationToken);
                (await before.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken))
                    .Should().ContainSingle().Which.Should().EndWith("InitialCreate");
            }

            await using (var upgrading = TestDatabase.CreateContextFor(filePath))
            {
                await CryptoStuffDatabase.MigrateAsync(upgrading, TestContext.Current.CancellationToken);
            }

            await using var verification = TestDatabase.CreateContextFor(filePath);
            var watchlist = await verification.Watchlists.SingleAsync(TestContext.Current.CancellationToken);
            watchlist.Name.Should().Be("Majors");
            watchlist.CoinIds.Should().Equal("bitcoin", "ethereum");
            (await verification.Portfolios.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
            (await verification.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken))
                .Should().Contain(name => name.EndsWith("AddPortfolios", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task Should_KeepWatchlistAndPortfolioData_When_UpgradingFromAddPortfoliosToAddPriceAlerts()
    {
        var filePath = Path.Combine(TestDatabase.Root, $"{Guid.NewGuid():N}.db");
        try
        {
            await using (var before = TestDatabase.CreateContextFor(filePath))
            {
                // Stops at the schema the portfolio release shipped, which
                // CryptoStuffDatabase offers no way to do.
                Directory.CreateDirectory(TestDatabase.Root);
                await before.GetService<IMigrator>().MigrateAsync("AddPortfolios", TestContext.Current.CancellationToken);
                await before.Database.ExecuteSqlRawAsync(
                    """INSERT INTO "Watchlists" ("Name", "CoinIds") VALUES ('Majors', '["bitcoin","ethereum"]')""",
                    TestContext.Current.CancellationToken);
                await before.Database.ExecuteSqlRawAsync(
                    """INSERT INTO "Portfolios" ("Name", "VsCurrency", "CostBasisMethod", "Version") VALUES ('Long term', 'eur', 'fifo', 2)""",
                    TestContext.Current.CancellationToken);
                await before.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO "PortfolioTransactions" ("Sequence", "Kind", "CoinId", "Quantity", "UnitPrice", "Fee", "OccurredAt", "PortfolioId")
                    VALUES (1, 'buy', 'bitcoin', '0.00000001', '40000.25', '0', '2026-01-05 12:30:15', 1)
                    """,
                    TestContext.Current.CancellationToken);
                (await before.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken))
                    .Should().HaveCount(2).And.Contain(name => name.EndsWith("AddPortfolios", StringComparison.Ordinal));
            }

            await using (var upgrading = TestDatabase.CreateContextFor(filePath))
            {
                await CryptoStuffDatabase.MigrateAsync(upgrading, TestContext.Current.CancellationToken);
            }

            await using var verification = TestDatabase.CreateContextFor(filePath);
            var watchlist = await verification.Watchlists.SingleAsync(TestContext.Current.CancellationToken);
            watchlist.Name.Should().Be("Majors");
            watchlist.CoinIds.Should().Equal("bitcoin", "ethereum");
            var portfolio = await verification.Portfolios.SingleAsync(TestContext.Current.CancellationToken);
            portfolio.Name.Should().Be("Long term");
            portfolio.Version.Should().Be(2);
            var transaction = portfolio.Transactions.Should().ContainSingle().Subject;
            transaction.CoinId.Should().Be("bitcoin");
            transaction.Quantity.Should().Be(0.00000001m);
            (await verification.Alerts.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
            (await verification.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken))
                .Should().Contain(name => name.EndsWith("AddPriceAlerts", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task Should_KeepWatchlistDataAtVersionZero_When_UpgradingFromAddPriceAlertsToAddWatchlistVersion()
    {
        var filePath = Path.Combine(TestDatabase.Root, $"{Guid.NewGuid():N}.db");
        try
        {
            await StoreWatchlistAtAddPriceAlertsAsync(filePath);

            await UpgradeAsync(filePath);

            // A watchlist stored before the upgrade takes the column's default,
            // 0, as its version; the token compares it like any other value.
            await using var verification = TestDatabase.CreateContextFor(filePath);
            var watchlist = await verification.Watchlists.SingleAsync(TestContext.Current.CancellationToken);
            watchlist.Name.Should().Be("Majors");
            watchlist.CoinIds.Should().Equal("bitcoin");
            watchlist.Version.Should().Be(0);
            (await verification.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken))
                .Should().Contain(name => name.EndsWith("AddWatchlistVersion", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task Should_SaveChangeAndMoveToVersionOne_When_ChangingWatchlistStoredBeforeAddWatchlistVersion()
    {
        var filePath = Path.Combine(TestDatabase.Root, $"{Guid.NewGuid():N}.db");
        try
        {
            await StoreWatchlistAtAddPriceAlertsAsync(filePath);
            await UpgradeAsync(filePath);
            await using var context = TestDatabase.CreateContextFor(filePath);
            (await context.Watchlists.SingleAsync(TestContext.Current.CancellationToken)).AddCoin("dogecoin");

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var verification = TestDatabase.CreateContextFor(filePath);
            var watchlist = await verification.Watchlists.SingleAsync(TestContext.Current.CancellationToken);
            watchlist.CoinIds.Should().Equal("bitcoin", "dogecoin");
            watchlist.Version.Should().Be(1);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task Should_ThrowDbUpdateConcurrencyExceptionAndKeepCompetingCoin_When_StaleChangeSavedToWatchlistStoredBeforeAddWatchlistVersion()
    {
        var filePath = Path.Combine(TestDatabase.Root, $"{Guid.NewGuid():N}.db");
        try
        {
            await StoreWatchlistAtAddPriceAlertsAsync(filePath);
            await UpgradeAsync(filePath);
            await using var stale = TestDatabase.CreateContextFor(filePath);
            var staleWatchlist = await stale.Watchlists.SingleAsync(TestContext.Current.CancellationToken);
            await using (var competing = TestDatabase.CreateContextFor(filePath))
            {
                (await competing.Watchlists.SingleAsync(TestContext.Current.CancellationToken)).AddCoin("dogecoin");
                await competing.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            staleWatchlist.AddCoin("solana");

            var act = () => stale.SaveChangesAsync(TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
            await using var verification = TestDatabase.CreateContextFor(filePath);
            (await verification.Watchlists.SingleAsync(TestContext.Current.CancellationToken))
                .CoinIds.Should().Equal("bitcoin", "dogecoin");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task Should_KeepEveryNameAndTransaction_When_UpgradingFromAddAlertAwaitingResetToUnicodeCaseInsensitiveNames()
    {
        var filePath = Path.Combine(TestDatabase.Root, $"{Guid.NewGuid():N}.db");
        try
        {
            await StoreAtAddAlertAwaitingResetAsync(
                filePath,
                """INSERT INTO "Watchlists" ("Name", "CoinIds", "Version") VALUES ('Ação', '["bitcoin"]', 3)""",
                """INSERT INTO "Portfolios" ("Name", "VsCurrency", "CostBasisMethod", "Version") VALUES ('Ação', 'eur', 'fifo', 2)""",
                """
                INSERT INTO "PortfolioTransactions" ("Sequence", "Kind", "CoinId", "Quantity", "UnitPrice", "Fee", "OccurredAt", "PortfolioId")
                VALUES (1, 'buy', 'bitcoin', '0.5', '40000', '0', '2026-01-05 12:30:15', 1)
                """);

            await UpgradeAsync(filePath);

            await using var verification = TestDatabase.CreateContextFor(filePath);
            var watchlist = await verification.Watchlists.SingleAsync(TestContext.Current.CancellationToken);
            watchlist.Name.Should().Be("Ação");
            watchlist.CoinIds.Should().Equal("bitcoin");
            watchlist.Version.Should().Be(3);
            var portfolio = await verification.Portfolios.SingleAsync(TestContext.Current.CancellationToken);
            portfolio.Name.Should().Be("Ação");
            portfolio.Version.Should().Be(2);
            portfolio.Transactions.Should().ContainSingle().Which.Quantity.Should().Be(0.5m);
            (await verification.Watchlists.AnyAsync(w => w.Name == "AÇÃO", TestContext.Current.CancellationToken)).Should().BeTrue();
            (await verification.Portfolios.AnyAsync(p => p.Name == "AÇÃO", TestContext.Current.CancellationToken)).Should().BeTrue();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData("Watchlists", """INSERT INTO "Watchlists" ("Name", "CoinIds", "Version") VALUES ('Ação', '[]', 1), ('AÇÃO', '[]', 1)""")]
    [InlineData("Portfolios", """INSERT INTO "Portfolios" ("Name", "VsCurrency", "CostBasisMethod", "Version") VALUES ('Ação', 'eur', 'fifo', 1), ('AÇÃO', 'eur', 'fifo', 1)""")]
    public async Task Should_RefuseTheUpgradeAndLeaveTheDatabaseAsItWas_When_TwoStoredNamesDifferOnlyInNonAsciiCase(string table, string insert)
    {
        var filePath = Path.Combine(TestDatabase.Root, $"{Guid.NewGuid():N}.db");
        try
        {
            await StoreAtAddAlertAwaitingResetAsync(filePath, insert);
            var schemaBefore = await ReadSchemaAsync(filePath);

            var act = () => UpgradeAsync(filePath);

            (await act.Should().ThrowAsync<SqliteException>()).Which.SqliteExtendedErrorCode.Should().Be(2067, "SQLITE_CONSTRAINT_UNIQUE");
            (await ReadSchemaAsync(filePath)).Should().Equal(
                schemaBefore,
                "the earlier release must still open the database, so the user can rename one of the two names with it");
            await using var verification = TestDatabase.CreateContextFor(filePath);
            var names = await verification.Database
                .SqlQueryRaw<string>(string.Concat("SELECT \"Name\" AS \"Value\" FROM \"", table, "\""))
                .ToListAsync(TestContext.Current.CancellationToken);
            names.Should().BeEquivalentTo("Ação", "AÇÃO");
            (await verification.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken))
                .Last().Should().EndWith("AddAlertAwaitingReset");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    // A portfolio stored before the upgrade has no record of a removed
    // newest transaction, so its next sequence follows the highest it still
    // holds: here sequence 2 was removed, and 3 is the newest left.
    [Fact]
    public async Task Should_NumberNextTransactionAfterTheHighestStoredSequence_When_UpgradingFromUnicodeCaseInsensitiveNamesToAddPortfolioLastSequence()
    {
        var filePath = Path.Combine(TestDatabase.Root, $"{Guid.NewGuid():N}.db");
        try
        {
            await StoreAtUnicodeCaseInsensitiveNamesAsync(
                filePath,
                """INSERT INTO "Portfolios" ("Name", "VsCurrency", "CostBasisMethod", "Version") VALUES ('Long term', 'usd', 'fifo', 4), ('Empty', 'usd', 'fifo', 1)""",
                """
                INSERT INTO "PortfolioTransactions" ("Sequence", "Kind", "CoinId", "Quantity", "UnitPrice", "Fee", "OccurredAt", "PortfolioId")
                VALUES (1, 'buy', 'bitcoin', '1', '40000', '0', '2026-01-05 12:30:15', 1),
                       (3, 'buy', 'bitcoin', '1', '41000', '0', '2026-01-07 12:30:15', 1)
                """);

            await UpgradeAsync(filePath);

            await using (var adding = TestDatabase.CreateContextFor(filePath))
            {
                foreach (var portfolio in await adding.Portfolios.ToListAsync(TestContext.Current.CancellationToken))
                {
                    portfolio.AddTransaction(new TransactionDetails
                    {
                        Kind = TransactionKind.Buy,
                        CoinId = "ethereum",
                        Quantity = 1m,
                        UnitPrice = 1m,
                        OccurredAt = new DateTimeOffset(2026, 1, 9, 0, 0, 0, TimeSpan.Zero),
                    }).IsValid.Should().BeTrue();
                }

                await adding.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var verification = TestDatabase.CreateContextFor(filePath);
            var portfolios = await verification.Portfolios.ToListAsync(TestContext.Current.CancellationToken);
            portfolios.Single(p => p.Name == "Long term").Transactions.Select(t => t.Sequence).Should().Equal(1, 3, 4);
            portfolios.Single(p => p.Name == "Empty").Transactions.Should().ContainSingle().Which.Sequence.Should().Be(1);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    // No release ever stored two transactions of one portfolio with the same
    // sequence, since the portfolio's version serialises its writers, but a
    // file edited by another tool could hold them.
    [Fact]
    public async Task Should_RefuseTheUpgradeAndLeaveTheDatabaseAsItWas_When_TwoStoredTransactionsOfOnePortfolioShareASequence()
    {
        var filePath = Path.Combine(TestDatabase.Root, $"{Guid.NewGuid():N}.db");
        try
        {
            await StoreAtUnicodeCaseInsensitiveNamesAsync(
                filePath,
                """INSERT INTO "Portfolios" ("Name", "VsCurrency", "CostBasisMethod", "Version") VALUES ('Long term', 'usd', 'fifo', 3)""",
                """
                INSERT INTO "PortfolioTransactions" ("Sequence", "Kind", "CoinId", "Quantity", "UnitPrice", "Fee", "OccurredAt", "PortfolioId")
                VALUES (1, 'buy', 'bitcoin', '1', '40000', '0', '2026-01-05 12:30:15', 1),
                       (1, 'buy', 'ethereum', '1', '3000', '0', '2026-01-06 12:30:15', 1)
                """);
            var schemaBefore = await ReadSchemaAsync(filePath);

            var act = () => UpgradeAsync(filePath);

            (await act.Should().ThrowAsync<SqliteException>()).Which.SqliteExtendedErrorCode.Should().Be(2067, "SQLITE_CONSTRAINT_UNIQUE");
            (await ReadSchemaAsync(filePath)).Should().Equal(
                schemaBefore,
                "the earlier release must still open the database, so the user can delete one of the two with it");
            await using var verification = TestDatabase.CreateContextFor(filePath);
            (await verification.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken))
                .Last().Should().EndWith("UnicodeCaseInsensitiveNames");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task Should_ThrowOperationCanceledException_When_MigrationCanceled()
    {
        var filePath = Path.Combine(TestDatabase.Root, $"{Guid.NewGuid():N}.db");
        await using var context = TestDatabase.CreateContextFor(filePath);
        try
        {
            var act = () => CryptoStuffDatabase.MigrateAsync(context, new CancellationToken(canceled: true));

            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    // Stops at the schema the price alert release shipped, which
    // CryptoStuffDatabase offers no way to do, and stores one watchlist in it.
    private static async Task StoreWatchlistAtAddPriceAlertsAsync(string filePath)
    {
        await using var before = TestDatabase.CreateContextFor(filePath);
        Directory.CreateDirectory(TestDatabase.Root);
        await before.GetService<IMigrator>().MigrateAsync("AddPriceAlerts", TestContext.Current.CancellationToken);
        await before.Database.ExecuteSqlRawAsync(
            """INSERT INTO "Watchlists" ("Name", "CoinIds") VALUES ('Majors', '["bitcoin"]')""",
            TestContext.Current.CancellationToken);
    }

    // Stops at the schema the alert re-arm release shipped and runs each
    // statement against it.
    private static async Task StoreAtAddAlertAwaitingResetAsync(string filePath, params string[] statements)
    {
        await using var before = TestDatabase.CreateContextFor(filePath);
        Directory.CreateDirectory(TestDatabase.Root);
        await before.GetService<IMigrator>().MigrateAsync("AddAlertAwaitingReset", TestContext.Current.CancellationToken);
        foreach (var statement in statements)
        {
            await before.Database.ExecuteSqlRawAsync(statement, TestContext.Current.CancellationToken);
        }
    }

    // Stops at the schema the Unicode-name release shipped and runs each
    // statement against it.
    private static async Task StoreAtUnicodeCaseInsensitiveNamesAsync(string filePath, params string[] statements)
    {
        await using var before = TestDatabase.CreateContextFor(filePath);
        Directory.CreateDirectory(TestDatabase.Root);
        await before.GetService<IMigrator>().MigrateAsync("UnicodeCaseInsensitiveNames", TestContext.Current.CancellationToken);
        foreach (var statement in statements)
        {
            await before.Database.ExecuteSqlRawAsync(statement, TestContext.Current.CancellationToken);
        }
    }

    // Every table, index and trigger with the SQL that created it, in name
    // order, read on a plain connection so nothing the context registers can
    // hide a difference.
    private static async Task<List<string>> ReadSchemaAsync(string filePath)
    {
        await using var connection = new SqliteConnection(TestDatabase.ConnectionStringFor(filePath));
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT type || ' ' || name || ': ' || coalesce(sql, '') FROM sqlite_master WHERE name NOT LIKE 'sqlite_%' ORDER BY name";
        var schema = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            schema.Add(reader.GetString(0));
        }

        return schema;
    }

    private static async Task UpgradeAsync(string filePath)
    {
        await using var upgrading = TestDatabase.CreateContextFor(filePath);
        await CryptoStuffDatabase.MigrateAsync(upgrading, TestContext.Current.CancellationToken);
    }

    private static CryptoStuffDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<CryptoStuffDbContext>().UseSqlite(connectionString).Options);

    // Stored tokens this version cannot read.

    [Theory]
    [InlineData("PriceAlerts", "Direction", "sideways")]
    [InlineData("PriceAlerts", "State", "snoozed")]
    [InlineData("Portfolios", "CostBasisMethod", "lifo")]
    [InlineData("PortfolioTransactions", "Kind", "transfer")]
    public async Task Should_RefuseNamingTheColumnAndValue_When_AStoredTokenIsUnknown(string table, string column, string token)
    {
        using var database = await TestDatabase.CreateMigratedAsync();
        await SeedAlertAndPortfolioAsync(database);
        var sql = string.Concat("UPDATE ", table, " SET ", column, " = '", token, "'");
        await using (var writer = database.CreateContext())
        {
            await writer.Database.ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken);
        }

        await using var context = database.CreateContext();

        var act = () => CryptoStuffDatabase.MigrateAsync(context, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InvalidDataException>()).WithMessage($"{table}.{column} holds '{token}'*newer version*");
    }

    [Fact]
    public async Task Should_Succeed_When_EveryStoredTokenIsKnown()
    {
        using var database = await TestDatabase.CreateMigratedAsync();
        await SeedAlertAndPortfolioAsync(database);
        await using var context = database.CreateContext();

        var act = () => CryptoStuffDatabase.MigrateAsync(context, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    private static async Task SeedAlertAndPortfolioAsync(TestDatabase database)
    {
        await using var context = database.CreateContext();
        context.Alerts.Add(PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m));
        var portfolio = Portfolio.Create("Long term", "usd", CostBasisMethod.Fifo);
        portfolio.AddTransaction(new TransactionDetails
        {
            Kind = TransactionKind.Buy,
            CoinId = "bitcoin",
            Quantity = 1m,
            UnitPrice = 100m,
            OccurredAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        }).IsValid.Should().BeTrue();
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
