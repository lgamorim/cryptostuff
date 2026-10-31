using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace CryptoStuff.Composition;

/// <summary>Locates the SQLite database both hosts share.</summary>
public static class DatabaseConnectionString
{
    /// <summary>The <c>ConnectionStrings</c> entry that overrides the default database file.</summary>
    public const string Name = "CryptoStuff";

    /// <summary>
    /// Returns <c>ConnectionStrings:CryptoStuff</c> when it is configured;
    /// otherwise a connection string for <c>cryptostuff/cryptostuff.db</c>
    /// under <paramref name="localApplicationDataPath"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The configured value is not a valid SQLite connection string, or none is
    /// configured and <paramref name="localApplicationDataPath"/> is blank.
    /// </exception>
    public static string Resolve(IConfiguration configuration, string? localApplicationDataPath)
    {
        var configured = configuration.GetConnectionString(Name);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Validate(configured);
        }

        if (string.IsNullOrWhiteSpace(localApplicationDataPath))
        {
            throw new InvalidOperationException(
                "No per-user local application-data folder is available for the default database file. "
                + "Set the ConnectionStrings:CryptoStuff setting (the ConnectionStrings__CryptoStuff "
                + "environment variable in deployment) to a SQLite connection string, e.g. "
                + "'Data Source=/path/to/cryptostuff.db'.");
        }

        // The builder quotes the path, so a folder name containing ';' or '='
        // cannot be misread as the start of another keyword.
        return new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(localApplicationDataPath, "cryptostuff", "cryptostuff.db"),
        }.ToString();
    }

    // Fails at registration rather than on the first query. The message names
    // the setting but never echoes its value, which may hold a password.
    private static string Validate(string connectionString)
    {
        try
        {
            _ = new SqliteConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:CryptoStuff is not a valid SQLite connection string: {exception.Message}",
                exception);
        }

        return connectionString;
    }
}
