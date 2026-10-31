using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CryptoStuff.Persistence;

/// <summary>Brings the application's database up to date.</summary>
public static class CryptoStuffDatabase
{
    private const string InMemoryDataSource = ":memory:";

    /// <summary>
    /// Applies every pending migration to <paramref name="context"/>'s database.
    /// For a file database, first creates the file's directory when it is
    /// missing, since SQLite creates the file but not the folders above it.
    /// </summary>
    public static async Task MigrateAsync(CryptoStuffDbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var directory = FileDatabaseDirectory(context.Database.GetConnectionString());
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        await context.Database.MigrateAsync(cancellationToken);
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
