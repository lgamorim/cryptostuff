using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CryptoStuff.Persistence.IntegrationTests.TestSupport;

/// <summary>
/// A migrated SQLite database file owned by one test: its own GUID-named file
/// under <c>&lt;OS temp&gt;/cryptostuff-tests</c>, so parallel tests never share
/// one, opened with <c>Pooling=False</c> so no pooled connection keeps the file
/// locked once a context is disposed. Disposing deletes the file. Assertions
/// read through <see cref="CreateContext"/>, a fresh context each time, so a
/// change tracker can never mask a write that did not reach the file.
/// </summary>
internal sealed class TestDatabase : IDisposable
{
    /// <summary>The folder every test database file lives in.</summary>
    public static readonly string Root = Path.Combine(Path.GetTempPath(), "cryptostuff-tests");

    private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(1);

    // Runs the first time any test asks for a database: once per test run.
    private static readonly Lazy<bool> SweptStaleEntries = new(SweepStaleEntries);

    private TestDatabase(string filePath)
    {
        FilePath = filePath;
        ConnectionString = ConnectionStringFor(filePath);
    }

    /// <summary>The database file's full path.</summary>
    public string FilePath { get; }

    /// <summary>The connection string every context of this database uses.</summary>
    public string ConnectionString { get; }

    /// <summary>Creates a fresh database file, migrated through <see cref="CryptoStuffDatabase.MigrateAsync"/>.</summary>
    public static async Task<TestDatabase> CreateMigratedAsync()
    {
        _ = SweptStaleEntries.Value;
        var database = new TestDatabase(Path.Combine(Root, $"{Guid.NewGuid():N}.db"));
        await using var context = database.CreateContext();
        await CryptoStuffDatabase.MigrateAsync(context, TestContext.Current.CancellationToken);
        return database;
    }

    /// <summary>The connection string for a test database file at <paramref name="filePath"/>.</summary>
    public static string ConnectionStringFor(string filePath) =>
        new SqliteConnectionStringBuilder { DataSource = filePath, Pooling = false }.ToString();

    /// <summary>A new context over the database file at <paramref name="filePath"/>.</summary>
    public static CryptoStuffDbContext CreateContextFor(string filePath) =>
        new(new DbContextOptionsBuilder<CryptoStuffDbContext>().UseSqlite(ConnectionStringFor(filePath)).Options);

    /// <summary>A new context over this database, with an empty change tracker.</summary>
    public CryptoStuffDbContext CreateContext() => CreateContextFor(FilePath);

    public void Dispose() => File.Delete(FilePath);

    // Removes what a crashed run left behind. Entries younger than a day are
    // kept, since they may belong to a run still in progress.
    private static bool SweepStaleEntries()
    {
        if (!Directory.Exists(Root))
        {
            return true;
        }

        var cutoff = TimeProvider.System.GetUtcNow().UtcDateTime - StaleAfter;
        foreach (var entry in new DirectoryInfo(Root).EnumerateFileSystemInfos())
        {
            if (entry.LastWriteTimeUtc >= cutoff)
            {
                continue;
            }

            try
            {
                if (entry is DirectoryInfo directory)
                {
                    directory.Delete(recursive: true);
                }
                else
                {
                    entry.Delete();
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Still open elsewhere; the next run's sweep retries it.
            }
        }

        return true;
    }
}
