using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace CryptoStuff.Composition;

/// <summary>Locates the SQLite database both hosts share.</summary>
public static class DatabaseConnectionString
{
    /// <summary>The <c>ConnectionStrings</c> entry that overrides the default database file.</summary>
    public const string Name = "CryptoStuff";

    private const string NotValidSqliteMessage =
        "ConnectionStrings:CryptoStuff is not a valid SQLite connection string. Check that every keyword is "
        + "one SQLite knows, that each value suits its keyword, and that any value holding ';' or '=' is quoted.";

    // One absolute path for each kind of OS: on Windows, a path starting at
    // '/' is relative to the current drive, so it is refused there too.
    private const string AbsolutePathExamples =
        @"'Data Source=C:\data\cryptostuff.db' on Windows or 'Data Source=/var/lib/cryptostuff/cryptostuff.db' elsewhere";

    private const string NotAnAbsoluteFileMessage =
        $"ConnectionStrings:CryptoStuff must name the database file by an absolute path, such as {AbsolutePathExamples}, "
        + "so the CLI and the API open the same file wherever they are launched from. Relative paths, in-memory "
        + "and temporary databases, file: URIs and |DataDirectory| are not supported.";

    /// <summary>
    /// Returns <c>ConnectionStrings:CryptoStuff</c> when it is configured;
    /// otherwise a connection string for <c>cryptostuff/cryptostuff.db</c>
    /// under <paramref name="localApplicationDataPath"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The configured value is not a valid SQLite connection string or does not
    /// name the database file by an absolute path, or none is configured and
    /// <paramref name="localApplicationDataPath"/> is blank.
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
                + "environment variable in deployment) to a SQLite connection string naming an absolute file, "
                + $"such as {AbsolutePathExamples}.");
        }

        // The builder quotes the path, so a folder name containing ';' or '='
        // cannot be misread as the start of another keyword.
        return new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(localApplicationDataPath, "cryptostuff", "cryptostuff.db"),
        }.ToString();
    }

    // Fails at registration rather than on the first query. The message names
    // the setting but never echoes its value, which may hold a password: not
    // even through the parser's message, which can quote the part of a password
    // after an unquoted ';', nor through the parser's exception as the inner
    // one, since an unhandled start-up exception prints its inner ones too.
    private static string Validate(string connectionString)
    {
        SqliteConnectionStringBuilder builder;
        try
        {
            builder = new SqliteConnectionStringBuilder(connectionString);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw new InvalidOperationException(NotValidSqliteMessage);
        }

        // Both hosts must open one file wherever they are launched from, and
        // only an absolute path does: a relative one resolves against each
        // host's working directory, and an in-memory or temporary database (no
        // data source) is gone with its connection. A file: URI or a
        // |DataDirectory| path is never fully qualified either.
        if (builder.Mode == SqliteOpenMode.Memory || !Path.IsPathFullyQualified(builder.DataSource))
        {
            throw new InvalidOperationException(NotAnAbsoluteFileMessage);
        }

        return connectionString;
    }
}
