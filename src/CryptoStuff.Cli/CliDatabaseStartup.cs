using Microsoft.Data.Sqlite;

namespace CryptoStuff.Cli;

/// <summary>
/// Runs the start-up migration for one CLI invocation and turns a database
/// that cannot be opened or migrated into an exit code and an error line,
/// rather than an unhandled exception's stack trace.
/// </summary>
public static class CliDatabaseStartup
{
    /// <summary>
    /// Runs <paramref name="migrate"/>, returning the exit code to stop with:
    /// <c>0</c> when it succeeded and the command may run, <c>1</c> after
    /// writing an error to <paramref name="error"/> when the database could not
    /// be opened or migrated, and <c>130</c> if
    /// <paramref name="cancellationToken"/> was canceled. Any other exception
    /// is a defect rather than an environment problem, and propagates.
    /// </summary>
    public static async Task<int> MigrateAsync(Func<CancellationToken, Task> migrate, TextWriter error, CancellationToken cancellationToken)
    {
        try
        {
            await migrate(cancellationToken);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
        // SqliteException: the file cannot be opened, is not a database, or is
        // read-only. IOException and UnauthorizedAccessException: its directory
        // cannot be created.
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            error.Write($"Error: the database could not be opened or migrated. {exception.Message}");
            error.Write('\n');
            return 1;
        }
    }
}
