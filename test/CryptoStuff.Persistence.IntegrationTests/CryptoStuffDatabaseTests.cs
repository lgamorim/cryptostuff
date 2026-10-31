using CryptoStuff.Core.Watchlists;
using CryptoStuff.Persistence.IntegrationTests.TestSupport;
using Microsoft.EntityFrameworkCore;

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

    private static CryptoStuffDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<CryptoStuffDbContext>().UseSqlite(connectionString).Options);
}
