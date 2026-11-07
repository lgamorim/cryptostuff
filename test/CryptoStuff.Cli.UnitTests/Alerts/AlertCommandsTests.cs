using System.Globalization;
using CryptoStuff.Cli.UnitTests.TestSupport;
using CryptoStuff.Core;
using CryptoStuff.Core.Alerts;

namespace CryptoStuff.Cli.UnitTests.Alerts;

public class AlertCommandsTests
{
    private readonly SpyCryptocurrencyService _market = new();
    private readonly SpyAlertService _alerts = new();
    private readonly SpyAlertEvaluator _evaluator = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private readonly CliRunner _runner;

    public AlertCommandsTests()
    {
        _runner = new CliRunner(_market, _output, _error, alerts: () => _alerts, alertEvaluator: () => _evaluator);
    }

    [Fact]
    public async Task Should_ReturnZeroAndListText_When_ListSucceeds()
    {
        // Arrange
        _alerts.ListResult = ServiceResult<IReadOnlyList<AlertView>>.Success(
        [
            Armed(id: 1, "bitcoin", "usd", AlertDirection.Above, 50000m),
            Triggered(id: 2, "ethereum", "eur", AlertDirection.Below, 1500.5m),
        ]);

        // Act
        var exitCode = await _runner.RunAsync(["alert", "list"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be("1: bitcoin above 50000 usd (armed)\n2: ethereum below 1500.5 eur (triggered)\n");
        _error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_PrintPlaceholder_When_ListIsEmpty()
    {
        // Arrange
        _alerts.ListResult = ServiceResult<IReadOnlyList<AlertView>>.Success([]);

        // Act
        var exitCode = await _runner.RunAsync(["alert", "list"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be("(no alerts)\n");
    }

    [Fact]
    public async Task Should_ReturnZeroAndJsonWithKebabCaseTokens_When_ListSucceedsWithJsonFlag()
    {
        // Arrange
        _alerts.ListResult = ServiceResult<IReadOnlyList<AlertView>>.Success(
        [
            Triggered(id: 2, "ethereum", "eur", AlertDirection.Below, 1500.5m),
        ]);

        // Act
        var exitCode = await _runner.RunAsync(["alert", "list", "--json"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should()
            .Contain("\"CoinId\": \"ethereum\"")
            .And.Contain("\"Direction\": \"below\"")
            .And.Contain("\"State\": \"triggered\"")
            .And.Contain("\"Threshold\": 1500.5")
            .And.Contain("\"TriggeredPrice\": 1490.25");
        _error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_PrintArmedAlertAndPassId_When_ShowSucceeds()
    {
        // Arrange
        _alerts.GetResult = ServiceResult<AlertView>.Success(Armed(id: 7, "bitcoin", "usd", AlertDirection.Above, 50000m));

        // Act
        var exitCode = await _runner.RunAsync(["alert", "show", "7"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _alerts.CapturedId.Should().Be(7);
        _output.ToString().Should().Be(
            "Id: 7\nCoin: bitcoin\nCurrency: usd\nDirection: above\nThreshold: 50000\nState: armed\nTriggered: never\nVersion: 1\n");
        _error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_PrintWhenAndAtWhatPriceItTriggered_When_ShownAlertIsTriggered()
    {
        // Arrange
        _alerts.GetResult = ServiceResult<AlertView>.Success(Triggered(id: 2, "ethereum", "eur", AlertDirection.Below, 1500.5m));

        // Act
        var exitCode = await _runner.RunAsync(["alert", "show", "2"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be(
            "Id: 2\nCoin: ethereum\nCurrency: eur\nDirection: below\nThreshold: 1500.5\nState: triggered\n"
            + "Triggered: 2024-03-01T12:00:00Z at 1490.25 eur\nVersion: 2\n");
    }

    [Fact]
    public async Task Should_PrintJsonWithKebabCaseTokens_When_ShowSucceedsWithJsonFlag()
    {
        // Arrange
        _alerts.GetResult = ServiceResult<AlertView>.Success(Triggered(id: 2, "ethereum", "eur", AlertDirection.Below, 1500.5m));

        // Act
        var exitCode = await _runner.RunAsync(["alert", "show", "2", "--json"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should()
            .Contain("\"Id\": 2")
            .And.Contain("\"Direction\": \"below\"")
            .And.Contain("\"State\": \"triggered\"")
            .And.Contain("\"TriggeredPrice\": 1490.25")
            .And.Contain("\"TriggeredAt\": \"2024-03-01T12:00:00Z\"")
            .And.Contain("\"Version\": 2");
    }

    [Fact]
    public async Task Should_CreateAlertAndPrintIt_When_CreateSucceeds()
    {
        // Arrange
        _alerts.CreateResult = ServiceResult<AlertView>.Success(Armed(id: 4, "bitcoin", "usd", AlertDirection.Below, 1500.25m));

        // Act
        var exitCode = await _runner.RunAsync(["alert", "create", "Bitcoin", "USD", "below", "1500.25"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _alerts.CapturedCreateCommand.Should().Be(new CreateAlertCommand
        {
            CoinId = "Bitcoin",
            VsCurrency = "USD",
            Direction = AlertDirection.Below,
            Threshold = 1500.25m,
        });
        _output.ToString().Should().StartWith("Id: 4\nCoin: bitcoin\nCurrency: usd\nDirection: below\nThreshold: 1500.25\n");
        _error.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData("sideways")]
    [InlineData("")]
    [InlineData(" above")]
    [InlineData("1")]
    public async Task Should_ReturnOneAndValidationError_When_CreateDirectionIsNotAToken(string rawDirection)
    {
        // Act
        var exitCode = await _runner.RunAsync(["alert", "create", "bitcoin", "usd", rawDirection, "100"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Direction must be 'above' or 'below'.\n");
        _output.ToString().Should().BeEmpty();
        _alerts.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("1e3")]
    [InlineData("1,5")]
    [InlineData("1,000.5")]
    [InlineData(" 5")]
    public async Task Should_ReturnOneAndValidationError_When_CreateThresholdIsNotANumber(string rawThreshold)
    {
        // Act
        var exitCode = await _runner.RunAsync(["alert", "create", "bitcoin", "usd", "above", rawThreshold], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Threshold must be a number.\n");
        _output.ToString().Should().BeEmpty();
        _alerts.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.0")]
    [InlineData("-1")]
    [InlineData("-0.5")]
    public async Task Should_ReturnOneAndValidationError_When_CreateThresholdIsNotPositive(string rawThreshold)
    {
        // Act
        var exitCode = await _runner.RunAsync(["alert", "create", "bitcoin", "usd", "above", rawThreshold], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Threshold must be greater than zero.\n");
        _output.ToString().Should().BeEmpty();
        _alerts.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("", "usd", "Coin is required.")]
    [InlineData("  ", "usd", "Coin is required.")]
    [InlineData("bitcoin", "", "Currency is required.")]
    [InlineData("bitcoin", "  ", "Currency is required.")]
    public async Task Should_ReturnOneAndValidationError_When_CreateCoinOrCurrencyIsBlank(string coin, string currency, string expectedError)
    {
        // Act
        var exitCode = await _runner.RunAsync(["alert", "create", coin, currency, "above", "100"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"{expectedError}\n");
        _alerts.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReadThresholdWithAPoint_When_CurrentCultureIsGerman()
    {
        // Arrange
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        try
        {
            // Act
            var exitCode = await _runner.RunAsync(["alert", "create", "bitcoin", "usd", "above", "1500.25"], CancellationToken.None);

            // Assert
            exitCode.Should().Be(0);
            _alerts.CapturedCreateCommand.Should().Be(new CreateAlertCommand
            {
                CoinId = "bitcoin",
                VsCurrency = "usd",
                Direction = AlertDirection.Above,
                Threshold = 1500.25m,
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("1500,25")]
    [InlineData("1.500,25")]
    public async Task Should_RejectCommaDecimalThreshold_When_CurrentCultureIsGerman(string rawThreshold)
    {
        // Arrange
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        try
        {
            // Act
            var exitCode = await _runner.RunAsync(["alert", "create", "bitcoin", "usd", "above", rawThreshold], CancellationToken.None);

            // Assert
            exitCode.Should().Be(1);
            _error.ToString().Should().Be("Threshold must be a number.\n");
            _alerts.CallCount.Should().Be(0);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task Should_PrintThresholdAndPriceWithAPoint_When_CurrentCultureIsGerman()
    {
        // Arrange
        _alerts.GetResult = ServiceResult<AlertView>.Success(Triggered(id: 2, "ethereum", "eur", AlertDirection.Below, 1234.5m));
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        try
        {
            // Act
            await _runner.RunAsync(["alert", "show", "2"], CancellationToken.None);

            // Assert
            _output.ToString().Should()
                .Contain("Threshold: 1234.5\n")
                .And.Contain("at 1490.25 eur");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task Should_PrintJson_When_CreateSucceedsWithJsonFlag()
    {
        // Arrange
        _alerts.CreateResult = ServiceResult<AlertView>.Success(Armed(id: 4, "bitcoin", "usd", AlertDirection.Above, 50000m));

        // Act
        var exitCode = await _runner.RunAsync(["alert", "create", "bitcoin", "usd", "above", "50000", "--json"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should()
            .Contain("\"Id\": 4")
            .And.Contain("\"Direction\": \"above\"")
            .And.Contain("\"State\": \"armed\"")
            .And.Contain("\"TriggeredAt\": null");
    }

    [Fact]
    public async Task Should_UpdateAlertWithoutExpectedVersionAndPrintIt_When_UpdateSucceeds()
    {
        // Arrange
        _alerts.UpdateResult = ServiceResult<AlertView>.Success(Armed(id: 3, "bitcoin", "usd", AlertDirection.Below, 42.5m) with { Version = 4 });

        // Act
        var exitCode = await _runner.RunAsync(["alert", "update", "3", "BELOW", "42.5"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _alerts.CapturedUpdateCommand.Should().Be(new UpdateAlertCommand
        {
            AlertId = 3,
            Direction = AlertDirection.Below,
            Threshold = 42.5m,
            ExpectedVersion = null,
        });
        _output.ToString().Should().StartWith("Id: 3\nCoin: bitcoin\nCurrency: usd\nDirection: below\nThreshold: 42.5\n")
            .And.EndWith("Version: 4\n");
        _error.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData("show", "abc", "Id must be a whole number.")]
    [InlineData("show", "0", "Id must be a positive whole number.")]
    [InlineData("show", "-3", "Id must be a positive whole number.")]
    [InlineData("show", "1.5", "Id must be a whole number.")]
    [InlineData("delete", "abc", "Id must be a whole number.")]
    [InlineData("delete", "0", "Id must be a positive whole number.")]
    [InlineData("delete", "-3", "Id must be a positive whole number.")]
    [InlineData("rearm", "abc", "Id must be a whole number.")]
    [InlineData("rearm", "0", "Id must be a positive whole number.")]
    [InlineData("rearm", "-3", "Id must be a positive whole number.")]
    [InlineData("update", "abc", "Id must be a whole number.")]
    [InlineData("update", "0", "Id must be a positive whole number.")]
    [InlineData("update", "-3", "Id must be a positive whole number.")]
    public async Task Should_ReturnOneAndValidationError_When_IdIsInvalid(string subcommand, string rawId, string expectedError)
    {
        // Arrange
        string[] rest = subcommand == "update" ? ["above", "100"] : [];

        // Act
        var exitCode = await _runner.RunAsync(["alert", subcommand, rawId, .. rest], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"{expectedError}\n");
        _output.ToString().Should().BeEmpty();
        _alerts.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("sideways")]
    [InlineData("")]
    [InlineData("armed")]
    public async Task Should_ReturnOneAndValidationError_When_UpdateDirectionIsNotAToken(string rawDirection)
    {
        // Act
        var exitCode = await _runner.RunAsync(["alert", "update", "3", rawDirection, "100"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Direction must be 'above' or 'below'.\n");
        _output.ToString().Should().BeEmpty();
        _alerts.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("abc", "Threshold must be a number.")]
    [InlineData("", "Threshold must be a number.")]
    [InlineData("1,5", "Threshold must be a number.")]
    [InlineData("1e3", "Threshold must be a number.")]
    [InlineData("0", "Threshold must be greater than zero.")]
    [InlineData("-1", "Threshold must be greater than zero.")]
    public async Task Should_ReturnOneAndValidationError_When_UpdateThresholdIsInvalid(string rawThreshold, string expectedError)
    {
        // Act
        var exitCode = await _runner.RunAsync(["alert", "update", "3", "above", rawThreshold], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"{expectedError}\n");
        _output.ToString().Should().BeEmpty();
        _alerts.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_PrintJson_When_UpdateSucceedsWithJsonFlag()
    {
        // Arrange
        _alerts.UpdateResult = ServiceResult<AlertView>.Success(Armed(id: 3, "bitcoin", "usd", AlertDirection.Below, 42.5m) with { Version = 4 });

        // Act
        var exitCode = await _runner.RunAsync(["alert", "update", "3", "below", "42.5", "--json"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should()
            .Contain("\"Id\": 3")
            .And.Contain("\"Direction\": \"below\"")
            .And.Contain("\"Threshold\": 42.5")
            .And.Contain("\"Version\": 4");
    }

    [Fact]
    public async Task Should_DeleteWithoutExpectedVersionAndPrintConfirmation_When_DeleteSucceeds()
    {
        // Act
        var exitCode = await _runner.RunAsync(["alert", "delete", "5"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _alerts.CapturedId.Should().Be(5);
        _alerts.CapturedExpectedVersion.Should().BeNull();
        _output.ToString().Should().Be("Deleted alert 5.\n");
        _error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_PrintJson_When_RearmSucceedsWithJsonFlag()
    {
        // Arrange
        _alerts.RearmResult = ServiceResult<AlertView>.Success(Armed(id: 2, "ethereum", "eur", AlertDirection.Below, 1500.5m) with { Version = 3 });

        // Act
        var exitCode = await _runner.RunAsync(["alert", "rearm", "2", "--json"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Contain("\"State\": \"armed\"").And.Contain("\"Version\": 3");
    }

    [Fact]
    public async Task Should_PrintDeletedJson_When_DeleteSucceedsWithJsonFlag()
    {
        // Act
        var exitCode = await _runner.RunAsync(["alert", "delete", "5", "--json"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Contain("\"Id\": 5").And.Contain("\"Deleted\": true");
    }

    [Fact]
    public async Task Should_RearmWithoutExpectedVersionAndPrintAlert_When_RearmSucceeds()
    {
        // Arrange
        _alerts.RearmResult = ServiceResult<AlertView>.Success(Armed(id: 2, "ethereum", "eur", AlertDirection.Below, 1500.5m) with { Version = 3 });

        // Act
        var exitCode = await _runner.RunAsync(["alert", "rearm", "2"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _alerts.CapturedId.Should().Be(2);
        _alerts.CapturedExpectedVersion.Should().BeNull();
        _output.ToString().Should().StartWith("Id: 2\nCoin: ethereum\n").And.Contain("State: armed\n").And.EndWith("Version: 3\n");
        _error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnZeroAndSayNothingTriggered_When_CheckTriggersNothing()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        var exitCode = await _runner.RunAsync(["alert", "check"], cancellationTokenSource.Token);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be("Nothing triggered.\n");
        _error.ToString().Should().BeEmpty();
        _evaluator.CallCount.Should().Be(1);
        _evaluator.CapturedCancellationToken.Should().Be(cancellationTokenSource.Token);
        _alerts.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnZeroAndPrintTheTriggeredAlert_When_CheckTriggersOne()
    {
        // Arrange
        _evaluator.Result = ServiceResult<AlertEvaluationView>.Success(new AlertEvaluationView
        {
            Triggered = [TriggeredAlert(id: 2, "ethereum", "eur", AlertDirection.Below, 1500.5m, price: 1490.25m)],
        });

        // Act
        var exitCode = await _runner.RunAsync(["alert", "check"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be(
            "Triggered 1 alert:\n  2: ethereum below 1500.5 eur, price 1490.25 eur, at 2024-03-01T12:00:00Z\n");
        _error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnZeroAndPrintEveryTriggeredAlertInOrder_When_CheckTriggersSeveral()
    {
        // Arrange
        _evaluator.Result = ServiceResult<AlertEvaluationView>.Success(new AlertEvaluationView
        {
            Triggered =
            [
                TriggeredAlert(id: 2, "ethereum", "eur", AlertDirection.Below, 1500.5m, price: 1490.25m),
                TriggeredAlert(id: 5, "bitcoin", "usd", AlertDirection.Above, 50000m, price: 50123m),
                TriggeredAlert(id: 9, "solana", "usd", AlertDirection.Above, 100m, price: 100m),
            ],
        });

        // Act
        var exitCode = await _runner.RunAsync(["alert", "check"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be(
            "Triggered 3 alerts:\n"
            + "  2: ethereum below 1500.5 eur, price 1490.25 eur, at 2024-03-01T12:00:00Z\n"
            + "  5: bitcoin above 50000 usd, price 50123 usd, at 2024-03-01T12:00:00Z\n"
            + "  9: solana above 100 usd, price 100 usd, at 2024-03-01T12:00:00Z\n");
    }

    [Fact]
    public async Task Should_PrintJsonWithKebabCaseTokens_When_CheckTriggersSeveralWithJsonFlag()
    {
        // Arrange
        _evaluator.Result = ServiceResult<AlertEvaluationView>.Success(new AlertEvaluationView
        {
            Triggered =
            [
                TriggeredAlert(id: 2, "ethereum", "eur", AlertDirection.Below, 1500.5m, price: 1490.25m),
                TriggeredAlert(id: 5, "bitcoin", "usd", AlertDirection.Above, 50000m, price: 50123m),
            ],
        });

        // Act
        var exitCode = await _runner.RunAsync(["alert", "check", "--json"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should()
            .Contain("\"Triggered\": [")
            .And.Contain("\"Direction\": \"below\"")
            .And.Contain("\"Direction\": \"above\"")
            .And.Contain("\"Price\": 1490.25")
            .And.Contain("\"TriggeredAt\": \"2024-03-01T12:00:00Z\"");
    }

    [Fact]
    public async Task Should_PrintAnEmptyTriggeredListAsJson_When_CheckTriggersNothingWithJsonFlag()
    {
        // Act
        var exitCode = await _runner.RunAsync(["alert", "check", "--json"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Contain("\"Triggered\": []");
        _output.ToString().Should().NotContain("Nothing triggered");
    }

    private static TriggeredAlertView TriggeredAlert(int id, string coin, string currency, AlertDirection direction, decimal threshold, decimal price) => new()
    {
        Id = id,
        CoinId = coin,
        VsCurrency = currency,
        Direction = direction,
        Threshold = threshold,
        Price = price,
        TriggeredAt = new DateTimeOffset(2024, 3, 1, 12, 0, 0, TimeSpan.Zero),
    };

    private static AlertView Armed(int id, string coin, string currency, AlertDirection direction, decimal threshold) => new()
    {
        Id = id,
        CoinId = coin,
        VsCurrency = currency,
        Direction = direction,
        Threshold = threshold,
        State = AlertState.Armed,
        TriggeredAt = null,
        TriggeredPrice = null,
        Version = 1,
    };

    private static AlertView Triggered(int id, string coin, string currency, AlertDirection direction, decimal threshold) =>
        Armed(id, coin, currency, direction, threshold) with
        {
            State = AlertState.Triggered,
            TriggeredAt = new DateTimeOffset(2024, 3, 1, 12, 0, 0, TimeSpan.Zero),
            TriggeredPrice = 1490.25m,
            Version = 2,
        };
}
