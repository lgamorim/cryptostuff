using Microsoft.Data.Sqlite;

namespace CryptoStuff.Persistence.IntegrationTests.TestSupport;

/// <summary>
/// Another writer holding a database file's write lock: a connection of its
/// own inside an immediate transaction, so every other write waits until this
/// is disposed, which closes the connection and rolls the transaction back.
/// </summary>
internal sealed class WriteLock : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private WriteLock(SqliteConnection connection) => _connection = connection;

    /// <summary>Takes the write lock of the database <paramref name="connectionString"/> names.</summary>
    public static async Task<WriteLock> HoldAsync(string connectionString, CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "BEGIN IMMEDIATE";
            await command.ExecuteNonQueryAsync(cancellationToken);
            return new WriteLock(connection);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
