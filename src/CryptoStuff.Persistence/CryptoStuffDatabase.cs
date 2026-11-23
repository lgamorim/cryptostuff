using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CryptoStuff.Persistence;

/// <summary>Brings the application's database up to date.</summary>
public static class CryptoStuffDatabase
{
    private const string InMemoryDataSource = ":memory:";

    /// <summary>
    /// Applies every pending migration to <paramref name="context"/>'s database,
    /// then checks that every stored enumeration token is one this version
    /// can read. For a file database, first creates the file's directory when
    /// it is missing, since SQLite creates the file but not the folders above it.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// A row holds a token this version does not know, as one written by a
    /// newer version would; the message names the table, column and value.
    /// </exception>
    public static async Task MigrateAsync(CryptoStuffDbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var directory = FileDatabaseDirectory(context.Database.GetConnectionString());
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        // A database with no file (in-memory or temporary) lives only as long
        // as its connection, so both steps share one; the check would
        // otherwise open a fresh, empty database. A file database is never
        // opened ahead of the migration: SQLite would create the file empty,
        // and EF then skips the create step that puts a new file in WAL mode.
        var holdConnection = directory is null;
        if (holdConnection)
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await context.Database.MigrateAsync(cancellationToken);
            await StoredTokens.EnsureReadableAsync(context, cancellationToken);
        }
        finally
        {
            if (holdConnection)
            {
                await context.Database.CloseConnectionAsync();
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="context"/>'s database opens and has no pending
    /// migration. Only reads: a missing file is reported not ready and is not
    /// created, nor is a missing directory. A database SQLite cannot open or
    /// read, such as a file that is not a SQLite database, is not ready rather
    /// than an exception; cancellation still throws.
    /// </summary>
    public static async Task<bool> IsReadyAsync(CryptoStuffDbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // EF checks that a file database exists before reading its migration
        // history, without opening it, so a missing file is never created here.
        try
        {
            var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);
            return !pending.Any();
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    // A null connection string parses to an empty data source, which, like an
    // in-memory one, names no file whose directory could be missing.
    private static string? FileDatabaseDirectory(string? connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (builder.Mode == SqliteOpenMode.Memory
            || string.IsNullOrEmpty(builder.DataSource)
            || builder.DataSource == InMemoryDataSource)
        {
            return null;
        }

        return Path.GetDirectoryName(Path.GetFullPath(builder.DataSource));
    }
}
