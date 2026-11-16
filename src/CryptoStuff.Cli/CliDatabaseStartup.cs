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
    /// <c>0</c> when it succeeded and the command may run, <c>130</c> if
    /// <paramref name="cancellationToken"/> was canceled, and <c>1</c> after
    /// writing an error through <paramref name="writer"/> for any other failure:
    /// the database holds data this version cannot read, or could not be
    /// opened or migrated for any reason, so start-up never ends in a stack
    /// trace.
    /// </summary>
    public static async Task<int> MigrateAsync(Func<CancellationToken, Task> migrate, CliResultWriter writer, CancellationToken cancellationToken)
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

        // A stored token this version does not know, as a newer version may
        // write; the message names the table, column and value.
        catch (InvalidDataException exception)
        {
            return writer.WriteError($"Error: the database holds data this version cannot read. {exception.Message}");
        }

        // Most often a SqliteException (the file cannot be opened, is not a
        // database, or is read-only) or an IOException or
        // UnauthorizedAccessException (its folder cannot be created), but EF
        // Core's own failures, such as a migration that cannot be applied, are
        // reported the same way.
        catch (Exception exception)
        {
            return writer.WriteError($"Error: the database could not be opened or migrated. {exception.Message}");
        }
    }
}
