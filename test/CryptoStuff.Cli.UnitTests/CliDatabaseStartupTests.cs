using Microsoft.Data.Sqlite;

namespace CryptoStuff.Cli.UnitTests;

public class CliDatabaseStartupTests
{
    private readonly StringWriter _error = new();
    private readonly CliResultWriter _writer;

    public CliDatabaseStartupTests()
    {
        _writer = new CliResultWriter(TextWriter.Null, _error);
    }

    public static TheoryData<Exception> DatabaseFailures => new()
    {
        new SqliteException("SQLite Error 14: 'unable to open database file'.", 14),
        new IOException("The file exists."),
        new UnauthorizedAccessException("Access to the path is denied."),

        // Any other failure, such as one of EF Core's own, is reported the same way.
        new InvalidOperationException("The model for context 'CryptoStuffDbContext' has pending changes."),
    };

    [Fact]
    public async Task Should_ReturnZeroAndWriteNothing_When_MigrationSucceeds()
    {
        var exitCode = await CliDatabaseStartup.MigrateAsync(_ => Task.CompletedTask, _writer, CancellationToken.None);

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
            _writer,
            cancellationTokenSource.Token);

        captured.Should().Be(cancellationTokenSource.Token);
    }

    [Theory]
    [MemberData(nameof(DatabaseFailures))]
    public async Task Should_ReturnOneAndWriteErrorLine_When_DatabaseCannotBeOpenedOrMigrated(Exception failure)
    {
        var exitCode = await CliDatabaseStartup.MigrateAsync(_ => Task.FromException(failure), _writer, CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"Error: the database could not be opened or migrated. {failure.Message}\n");
    }

    [Fact]
    public async Task Should_ReturnOne_When_MigrationThrowsSynchronously()
    {
        var exitCode = await CliDatabaseStartup.MigrateAsync(
            _ => throw new SqliteException("SQLite Error 26: 'file is not a database'.", 26),
            _writer,
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
            _writer,
            cancellationTokenSource.Token);

        exitCode.Should().Be(130);
        _error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnOneAndWriteErrorLine_When_TheDatabaseHoldsDataThisVersionCannotRead()
    {
        var failure = new InvalidDataException("PriceAlerts.State holds 'snoozed', which is not an alert state this version knows.");

        var exitCode = await CliDatabaseStartup.MigrateAsync(_ => Task.FromException(failure), _writer, CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"Error: the database holds data this version cannot read. {failure.Message}\n");
    }
}
