using CryptoStuff.Cli.UnitTests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CryptoStuff.Cli.UnitTests;

public class StoredFeatureMigrationTests
{
    private readonly SpyCryptocurrencyService _market = new();
    private readonly SpyWatchlistService _watchlists = new();
    private readonly SpyPortfolioService _portfolios = new();
    private readonly SpyAlertService _alerts = new();
    private readonly SpyAlertEvaluator _evaluator = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    // What happened, in order: each migration, and each stored-feature service
    // resolved.
    private readonly List<string> _events = [];

    public static TheoryData<string[]> StoredFeatureCommands => new(
        ["watchlist", "list"],
        ["portfolio", "list"],
        ["portfolio", "tx", "delete", "1", "2"],
        ["alert", "list"],
        ["alert", "check"]);

    [Theory]
    [MemberData(nameof(StoredFeatureCommands))]
    public async Task Should_MigrateOnceBeforeResolvingAnyService_When_CommandIsAStoredFeature(string[] args)
    {
        // Arrange
        var runner = Runner(_ =>
        {
            _events.Add("migrate");
            return Task.CompletedTask;
        });

        // Act
        var exitCode = await runner.RunAsync(args, CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _events.Should().StartWith("migrate");
        _events.Should().ContainSingle(e => e == "migrate");
        _events.Should().Contain(e => e.StartsWith("resolve ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_PassTheCancellationTokenToTheMigration_When_CommandIsAStoredFeature()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var captured = new List<CancellationToken>();
        var runner = Runner(cancellationToken =>
        {
            captured.Add(cancellationToken);
            return Task.CompletedTask;
        });

        // Act
        await runner.RunAsync(["watchlist", "list"], cancellationTokenSource.Token);

        // Assert
        captured.Should().Equal(cancellationTokenSource.Token);
    }

    [Theory]
    [InlineData("price", "bitcoin", "usd")]
    [InlineData("token", "ethereum", "0xabc", "usd")]
    [InlineData("history", "bitcoin", "usd", "7")]
    [InlineData("coin", "bitcoin")]
    [InlineData("developer", "bitcoin", "31-01-2024")]
    public async Task Should_NeitherMigrateNorResolveAnyService_When_CommandOnlyUsesTheMarketDataProvider(params string[] args)
    {
        // Arrange
        var runner = Runner(MigrationThatRecords());

        // Act
        await runner.RunAsync(args, CancellationToken.None);

        // Assert
        _events.Should().BeEmpty();
    }

    [Theory]
    [InlineData]
    [InlineData("bogus")]
    [InlineData("WATCHLIST")]
    [InlineData("PORTFOLIO")]
    [InlineData("ALERT")]
    public async Task Should_NeitherMigrateNorResolveAnyService_When_CommandIsMissingOrUnrecognized(params string[] args)
    {
        // Arrange
        var runner = Runner(MigrationThatRecords());

        // Act
        var exitCode = await runner.RunAsync(args, CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Contain("Usage:");
        _events.Should().BeEmpty();
    }

    // Each flag is checked before anything runs, so an invocation it does not
    // belong to never touches the database.
    [Theory]
    [InlineData("watchlist", "list", "--fee", "1")]
    [InlineData("portfolio", "list", "--fee", "1")]
    [InlineData("alert", "check", "--fee", "1")]
    [InlineData("watchlist", "list", "--exit-code")]
    [InlineData("portfolio", "list", "--exit-code")]
    [InlineData("alert", "list", "--exit-code")]
    public async Task Should_ReturnOneAndUsageWithoutMigrating_When_StoredFeatureIsGivenAFlagItDoesNotTake(params string[] args)
    {
        // Arrange
        var runner = Runner(MigrationThatRecords());

        // Act
        var exitCode = await runner.RunAsync(args, CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Contain("Usage:");
        _output.ToString().Should().BeEmpty();
        _events.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(StoredFeatureCommands))]
    public async Task Should_ReturnOneWriteTheErrorAndResolveNoService_When_TheMigrationFails(string[] args)
    {
        // Arrange
        var failure = new SqliteException("SQLite Error 26: 'file is not a database'.", 26);
        var runner = Runner(_ => Task.FromException(failure));

        // Act
        var exitCode = await runner.RunAsync(args, CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"Error: the database could not be opened or migrated. {failure.Message}\n");
        _output.ToString().Should().BeEmpty();
        _events.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Return130WriteNothingAndResolveNoService_When_TheMigrationIsCanceled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        var runner = Runner(cancellationToken => Task.FromCanceled(cancellationToken));

        // Act
        var exitCode = await runner.RunAsync(["portfolio", "list"], cancellationTokenSource.Token);

        // Assert
        exitCode.Should().Be(130);
        _error.ToString().Should().BeEmpty();
        _output.ToString().Should().BeEmpty();
        _events.Should().BeEmpty();
    }

    private Func<CancellationToken, Task> MigrationThatRecords() => _ =>
    {
        _events.Add("migrate");
        return Task.CompletedTask;
    };

    private CliRunner Runner(Func<CancellationToken, Task> migrate)
    {
        var storedFeatures = TestStoredFeatures.With(
            watchlists: () => Resolve(_watchlists),
            portfolios: () => Resolve(_portfolios),
            alerts: () => Resolve(_alerts),
            alertEvaluator: () => Resolve(_evaluator),
            migrate: migrate);
        return new CliRunner(_market, storedFeatures, _output, _error);
    }

    private T Resolve<T>(T service)
        where T : notnull
    {
        _events.Add($"resolve {typeof(T).Name}");
        return service;
    }
}
