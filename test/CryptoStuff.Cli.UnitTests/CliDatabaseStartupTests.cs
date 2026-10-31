using Microsoft.Data.Sqlite;

namespace CryptoStuff.Cli.UnitTests;

public class CliDatabaseStartupTests
{
    private readonly StringWriter _error = new();

    public static TheoryData<Exception> DatabaseFailures => new()
    {
        new SqliteException("SQLite Error 14: 'unable to open database file'.", 14),
        new IOException("The file exists."),
        new UnauthorizedAccessException("Access to the path is denied."),
    };

    [Fact]
    public async Task Should_ReturnZeroAndWriteNothing_When_MigrationSucceeds()
    {
        var exitCode = await CliDatabaseStartup.MigrateAsync(_ => Task.CompletedTask, _error, CancellationToken.None);

        exitCode.Should().Be(0);
        _error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_PassCancellationTokenToMigration_When_Migrating()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken captured = default;

        await CliDatabaseStartup.MigrateAsync(
            cancellationToken =>
            {
                captured = cancellationToken;
                return Task.CompletedTask;
            },
            _error,
            cancellationTokenSource.Token);

        captured.Should().Be(cancellationTokenSource.Token);
    }

    [Theory]
    [MemberData(nameof(DatabaseFailures))]
    public async Task Should_ReturnOneAndWriteErrorLine_When_DatabaseCannotBeOpenedOrMigrated(Exception failure)
    {
        var exitCode = await CliDatabaseStartup.MigrateAsync(_ => Task.FromException(failure), _error, CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"Error: the database could not be opened or migrated. {failure.Message}\n");
    }

    [Fact]
    public async Task Should_ReturnOne_When_MigrationThrowsSynchronously()
    {
        var exitCode = await CliDatabaseStartup.MigrateAsync(
            _ => throw new SqliteException("SQLite Error 26: 'file is not a database'.", 26),
            _error,
            CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().StartWith("Error: the database could not be opened or migrated.");
    }

    [Fact]
    public async Task Should_Return130AndWriteNothing_When_MigrationCanceled()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var exitCode = await CliDatabaseStartup.MigrateAsync(
            cancellationToken => Task.FromCanceled(cancellationToken),
            _error,
            cancellationTokenSource.Token);

        exitCode.Should().Be(130);
        _error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_PropagateException_When_FailureIsNotADatabaseFailure()
    {
        var act = () => CliDatabaseStartup.MigrateAsync(
            _ => Task.FromException(new InvalidOperationException("A bug, not a database problem.")),
            _error,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
