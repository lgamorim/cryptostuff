using CryptoStuff.Cli.UnitTests.TestSupport;

namespace CryptoStuff.Cli.UnitTests.Watchlists;

public class WatchlistDispatchTests
{
    private readonly SpyCryptocurrencyService _market = new();
    private readonly SpyWatchlistService _watchlists = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    [Theory]
    [InlineData("watchlist list")]
    [InlineData("watchlist create <name>")]
    [InlineData("watchlist show <id>")]
    [InlineData("watchlist rename <id> <name>")]
    [InlineData("watchlist delete <id>")]
    [InlineData("watchlist add <id> <coin>")]
    [InlineData("watchlist remove <id> <coin>")]
    [InlineData("watchlist prices <id> <currency>")]
    public async Task Should_ListWatchlistCommandsInUsage_When_NoCommandGiven(string expectedLine)
    {
        // Arrange
        var runner = new CliRunner(_market, _output, _error, () => _watchlists);

        // Act
        var exitCode = await runner.RunAsync([], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Contain(expectedLine);
    }

    [Fact]
    public async Task Should_ReturnOneAndUsage_When_WatchlistServiceIsNotAvailable()
    {
        // Arrange
        var runner = new CliRunner(_market, _output, _error);

        // Act
        var exitCode = await runner.RunAsync(["watchlist", "list"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Contain("Usage:");
        _watchlists.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("price", "bitcoin", "usd")]
    [InlineData("coin", "bitcoin")]
    [InlineData("bogus")]
    public async Task Should_NotResolveWatchlistService_When_CommandIsNotWatchlist(params string[] args)
    {
        // Arrange
        var resolved = false;
        var runner = new CliRunner(_market, _output, _error, () =>
        {
            resolved = true;
            return _watchlists;
        });

        // Act
        await runner.RunAsync(args, CancellationToken.None);

        // Assert
        resolved.Should().BeFalse();
    }

    [Fact]
    public async Task Should_NotCallCryptocurrencyService_When_WatchlistCommandRuns()
    {
        // Arrange
        var runner = new CliRunner(_market, _output, _error, () => _watchlists);

        // Act
        await runner.RunAsync(["watchlist", "list"], CancellationToken.None);

        // Assert
        _watchlists.CallCount.Should().Be(1);
        _market.CapturedPricesQuery.Should().BeNull();
    }

    [Theory]
    [InlineData]
    [InlineData("bogus")]
    [InlineData("LIST")]
    [InlineData("list", "extra")]
    [InlineData("create")]
    [InlineData("create", "a", "b")]
    [InlineData("show")]
    [InlineData("show", "1", "2")]
    [InlineData("rename", "1")]
    [InlineData("rename", "1", "a", "b")]
    [InlineData("delete")]
    [InlineData("delete", "1", "2")]
    [InlineData("add", "1")]
    [InlineData("add", "1", "bitcoin", "extra")]
    [InlineData("remove", "1")]
    [InlineData("remove", "1", "bitcoin", "extra")]
    [InlineData("prices", "1")]
    [InlineData("prices", "1", "usd", "extra")]
    public async Task Should_ReturnOneAndUsageWithoutCallingService_When_SubcommandIsMissingUnknownOrMalformed(params string[] subcommand)
    {
        // Arrange
        var runner = new CliRunner(_market, _output, _error, () => _watchlists);

        // Act
        var exitCode = await runner.RunAsync(["watchlist", .. subcommand], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Contain("Usage:");
        _output.ToString().Should().BeEmpty();
        _watchlists.CallCount.Should().Be(0);
    }
}
