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

    [Theory]
    [InlineData("price")]
    [InlineData("token")]
    [InlineData("history")]
    [InlineData("coin")]
    [InlineData("developer")]
    public async Task Should_NotInvokeMigration_When_CommandOnlyUsesCoinGecko(string command)
    {
        var migrated = false;

        var exitCode = await CliDatabaseStartup.MigrateIfRequiredAsync(
            CliArgumentParser.Parse([command, "bitcoin", "usd"]),
            _ =>
            {
                migrated = true;
                return Task.CompletedTask;
            },
            _error,
            CancellationToken.None);

        exitCode.Should().Be(0);
        migrated.Should().BeFalse();
    }

    [Fact]
    public async Task Should_InvokeMigrationOnceWithToken_When_CommandUsesTheDatabase()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var captured = new List<CancellationToken>();

        var exitCode = await CliDatabaseStartup.MigrateIfRequiredAsync(
            CliArgumentParser.Parse(["watchlist", "list"]),
            cancellationToken =>
            {
                captured.Add(cancellationToken);
                return Task.CompletedTask;
            },
            _error,
            cancellationTokenSource.Token);

        exitCode.Should().Be(0);
        captured.Should().Equal(cancellationTokenSource.Token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("bogus")]
    [InlineData("WATCHLIST")]
    [InlineData("PORTFOLIO")]
    public async Task Should_NotInvokeMigration_When_CommandIsMissingOrUnrecognized(string? command)
    {
        var migrated = false;
        string[] args = command is null ? [] : [command];

        var exitCode = await CliDatabaseStartup.MigrateIfRequiredAsync(
            CliArgumentParser.Parse(args),
            _ =>
            {
                migrated = true;
                return Task.CompletedTask;
            },
            _error,
            CancellationToken.None);

        exitCode.Should().Be(0);
        migrated.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReturnOneAndWriteErrorLine_When_DatabaseCommandCannotMigrate()
    {
        var failure = new SqliteException("SQLite Error 26: 'file is not a database'.", 26);

        var exitCode = await CliDatabaseStartup.MigrateIfRequiredAsync(
            CliArgumentParser.Parse(["watchlist", "list"]),
            _ => Task.FromException(failure),
            _error,
            CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"Error: the database could not be opened or migrated. {failure.Message}\n");
    }

    [Fact]
    public async Task Should_Return130_When_DatabaseCommandMigrationCanceled()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var exitCode = await CliDatabaseStartup.MigrateIfRequiredAsync(
            CliArgumentParser.Parse(["watchlist", "list"]),
            cancellationToken => Task.FromCanceled(cancellationToken),
            _error,
            cancellationTokenSource.Token);

        exitCode.Should().Be(130);
        _error.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData("watchlist", "list")]
    [InlineData("portfolio", "list")]
    [InlineData("portfolio", "tx", "delete", "1", "2")]
    public async Task Should_InvokeMigrationOnce_When_CommandIsAStoredFeature(params string[] args)
    {
        var migrations = 0;

        var exitCode = await CliDatabaseStartup.MigrateIfRequiredAsync(
            CliArgumentParser.Parse(args),
            _ =>
            {
                migrations++;
                return Task.CompletedTask;
            },
            _error,
            CancellationToken.None);

        exitCode.Should().Be(0);
        migrations.Should().Be(1);
    }
}
