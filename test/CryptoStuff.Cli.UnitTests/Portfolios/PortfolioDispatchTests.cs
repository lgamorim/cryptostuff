using CryptoStuff.Cli.UnitTests.TestSupport;

namespace CryptoStuff.Cli.UnitTests.Portfolios;

public class PortfolioDispatchTests
{
    private readonly SpyCryptocurrencyService _market = new();
    private readonly SpyPortfolioService _portfolios = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    [Theory]
    [InlineData("portfolio list")]
    [InlineData("portfolio create <name> <currency> <average-cost|fifo>")]
    [InlineData("portfolio show <id>")]
    [InlineData("portfolio update <id> <name> <average-cost|fifo>")]
    [InlineData("portfolio delete <id>")]
    [InlineData("portfolio value <id>")]
    [InlineData("portfolio tx add <id> <buy|sell> <coin> <qty> <price> <when> [--fee <fee>]")]
    [InlineData("portfolio tx edit <id> <txid> <buy|sell> <coin> <qty> <price> <when> [--fee <fee>]")]
    [InlineData("portfolio tx delete <id> <txid>")]
    public async Task Should_ListPortfolioCommandsInUsage_When_NoCommandGiven(string expectedLine)
    {
        // Arrange
        var runner = new CliRunner(_market, _output, _error, portfolios: () => _portfolios);

        // Act
        var exitCode = await runner.RunAsync([], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Contain(expectedLine);
    }

    [Fact]
    public async Task Should_ReturnOneAndUsage_When_PortfolioServiceIsNotAvailable()
    {
        // Arrange
        var runner = new CliRunner(_market, _output, _error);

        // Act
        var exitCode = await runner.RunAsync(["portfolio", "list"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Contain("Usage:");
        _portfolios.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("price", "bitcoin", "usd")]
    [InlineData("coin", "bitcoin")]
    [InlineData("watchlist", "list")]
    [InlineData("bogus")]
    public async Task Should_NotResolvePortfolioService_When_CommandIsNotPortfolio(params string[] args)
    {
        // Arrange
        var resolved = false;
        var runner = new CliRunner(_market, _output, _error, portfolios: () =>
        {
            resolved = true;
            return _portfolios;
        });

        // Act
        await runner.RunAsync(args, CancellationToken.None);

        // Assert
        resolved.Should().BeFalse();
    }

    [Fact]
    public async Task Should_NotCallCryptocurrencyService_When_PortfolioCommandRuns()
    {
        // Arrange
        var runner = new CliRunner(_market, _output, _error, portfolios: () => _portfolios);

        // Act
        await runner.RunAsync(["portfolio", "value", "1"], CancellationToken.None);

        // Assert
        _portfolios.CallCount.Should().Be(1);
        _market.CapturedPricesQuery.Should().BeNull();
    }

    [Theory]
    [InlineData]
    [InlineData("bogus")]
    [InlineData("LIST")]
    [InlineData("list", "extra")]
    [InlineData("create")]
    [InlineData("create", "main", "usd")]
    [InlineData("create", "main", "usd", "fifo", "extra")]
    [InlineData("show")]
    [InlineData("show", "1", "2")]
    [InlineData("update", "1", "main")]
    [InlineData("update", "1", "main", "fifo", "extra")]
    [InlineData("delete")]
    [InlineData("delete", "1", "2")]
    [InlineData("value")]
    [InlineData("value", "1", "2")]
    [InlineData("tx")]
    [InlineData("tx", "bogus")]
    [InlineData("tx", "add")]
    [InlineData("tx", "add", "1", "buy", "bitcoin", "1", "1")]
    [InlineData("tx", "add", "1", "buy", "bitcoin", "1", "1", "2024-01-31T14:30:00Z", "extra")]
    [InlineData("tx", "edit", "1", "2", "buy", "bitcoin", "1", "1")]
    [InlineData("tx", "edit", "1", "2", "buy", "bitcoin", "1", "1", "2024-01-31T14:30:00Z", "extra")]
    [InlineData("tx", "delete", "1")]
    [InlineData("tx", "delete", "1", "2", "3")]
    [InlineData("tx", "add", "1", "buy", "bitcoin", "1", "1", "2024-01-31T14:30:00Z", "--fee")]
    [InlineData("tx", "add", "1", "buy", "bitcoin", "1", "1", "2024-01-31T14:30:00Z", "--fee", "1", "--fee", "2")]
    public async Task Should_ReturnOneAndUsageWithoutCallingService_When_SubcommandIsMissingUnknownOrMalformed(params string[] subcommand)
    {
        // Arrange
        var runner = new CliRunner(_market, _output, _error, portfolios: () => _portfolios);

        // Act
        var exitCode = await runner.RunAsync(["portfolio", .. subcommand], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Contain("Usage:");
        _output.ToString().Should().BeEmpty();
        _portfolios.CallCount.Should().Be(0);
    }
}
