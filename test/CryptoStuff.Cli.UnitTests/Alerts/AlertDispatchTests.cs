using CryptoStuff.Cli.UnitTests.TestSupport;

namespace CryptoStuff.Cli.UnitTests.Alerts;

public class AlertDispatchTests
{
    private readonly SpyCryptocurrencyService _market = new();
    private readonly SpyAlertService _alerts = new();
    private readonly SpyAlertEvaluator _evaluator = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    [Theory]
    [InlineData("alert list")]
    [InlineData("alert create <coin> <currency> <above|below> <threshold>")]
    [InlineData("alert show <id>")]
    [InlineData("alert update <id> <above|below> <threshold>")]
    [InlineData("alert delete <id>")]
    [InlineData("alert rearm <id>")]
    [InlineData("alert check")]
    public async Task Should_ListAlertCommandsInUsage_When_NoCommandGiven(string expectedLine)
    {
        // Arrange
        var runner = new CliRunner(_market, _output, _error, alerts: () => _alerts, alertEvaluator: () => _evaluator);

        // Act
        var exitCode = await runner.RunAsync([], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Contain(expectedLine);
    }

    [Fact]
    public async Task Should_ReturnOneAndUsage_When_AlertServicesAreNotAvailable()
    {
        // Arrange
        var runner = new CliRunner(_market, _output, _error);

        // Act
        var exitCode = await runner.RunAsync(["alert", "list"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Contain("Usage:");
        _alerts.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnOneAndUsage_When_OnlyTheAlertServiceIsAvailable()
    {
        // Arrange
        var runner = new CliRunner(_market, _output, _error, alerts: () => _alerts);

        // Act
        var exitCode = await runner.RunAsync(["alert", "check"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Contain("Usage:");
        _evaluator.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("price", "bitcoin", "usd")]
    [InlineData("coin", "bitcoin")]
    [InlineData("watchlist", "list")]
    [InlineData("portfolio", "list")]
    [InlineData("bogus")]
    public async Task Should_NotResolveAlertServices_When_CommandIsNotAlert(params string[] args)
    {
        // Arrange
        var resolved = false;
        var runner = new CliRunner(
            _market,
            _output,
            _error,
            alerts: () =>
            {
                resolved = true;
                return _alerts;
            },
            alertEvaluator: () =>
            {
                resolved = true;
                return _evaluator;
            });

        // Act
        await runner.RunAsync(args, CancellationToken.None);

        // Assert
        resolved.Should().BeFalse();
    }

    [Fact]
    public async Task Should_NotCallCryptocurrencyService_When_AlertCommandRuns()
    {
        // Arrange
        var runner = new CliRunner(_market, _output, _error, alerts: () => _alerts, alertEvaluator: () => _evaluator);

        // Act
        await runner.RunAsync(["alert", "check"], CancellationToken.None);

        // Assert
        _evaluator.CallCount.Should().Be(1);
        _market.CapturedPricesQuery.Should().BeNull();
    }

    [Theory]
    [InlineData]
    [InlineData("bogus")]
    [InlineData("LIST")]
    [InlineData("list", "extra")]
    [InlineData("create")]
    [InlineData("create", "bitcoin", "usd", "above")]
    [InlineData("create", "bitcoin", "usd", "above", "100", "extra")]
    [InlineData("show")]
    [InlineData("show", "1", "2")]
    [InlineData("update", "1", "above")]
    [InlineData("update", "1", "above", "100", "extra")]
    [InlineData("delete")]
    [InlineData("delete", "1", "2")]
    [InlineData("rearm")]
    [InlineData("rearm", "1", "2")]
    [InlineData("check", "extra")]
    [InlineData("check", "1")]
    public async Task Should_ReturnOneAndUsageWithoutCallingAnything_When_SubcommandIsMissingUnknownOrMalformed(params string[] subcommand)
    {
        // Arrange
        var runner = new CliRunner(_market, _output, _error, alerts: () => _alerts, alertEvaluator: () => _evaluator);

        // Act
        var exitCode = await runner.RunAsync(["alert", .. subcommand], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Contain("Usage:");
        _output.ToString().Should().BeEmpty();
        _alerts.CallCount.Should().Be(0);
        _evaluator.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("check")]
    [InlineData("rearm", "1")]
    public async Task Should_ReturnOneAndUsage_When_FeeOptionIsGiven(params string[] subcommand)
    {
        // Arrange
        var runner = new CliRunner(_market, _output, _error, alerts: () => _alerts, alertEvaluator: () => _evaluator);

        // Act
        var exitCode = await runner.RunAsync(["alert", .. subcommand, "--fee", "1"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Contain("Usage:");
        _alerts.CallCount.Should().Be(0);
        _evaluator.CallCount.Should().Be(0);
    }
}
