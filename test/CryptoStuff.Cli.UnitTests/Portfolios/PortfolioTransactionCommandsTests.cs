using System.Globalization;
using CryptoStuff.Cli.UnitTests.TestSupport;
using CryptoStuff.Core;
using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Cli.UnitTests.Portfolios;

public class PortfolioTransactionCommandsTests
{
    private static readonly DateTimeOffset Moment = new(2024, 1, 31, 14, 30, 0, TimeSpan.Zero);

    private readonly SpyCryptocurrencyService _market = new();
    private readonly SpyPortfolioService _portfolios = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private readonly CliRunner _runner;

    public PortfolioTransactionCommandsTests()
    {
        _runner = new CliRunner(_market, _output, _error, portfolios: () => _portfolios);
    }

    [Fact]
    public async Task Should_AddTransactionWithZeroFeeAndPrintPortfolio_When_FeeIsAbsent()
    {
        // Arrange
        _portfolios.AddTransactionResult = ServiceResult<PortfolioView>.Success(new PortfolioView
        {
            Id = 3,
            Name = "main",
            VsCurrency = "usd",
            CostBasisMethod = CostBasisMethod.Fifo,
            Version = 2,
            Transactions = [],
        });

        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "tx", "add", "3", "buy", "Bitcoin", "0.5", "40000.25", "2024-01-31T14:30:00Z"],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().StartWith("Id: 3\nName: main\n");
        _portfolios.CapturedAddTransactionCommand.Should().Be(new AddTransactionCommand
        {
            PortfolioId = 3,
            Transaction = new TransactionDetails
            {
                Kind = TransactionKind.Buy,
                CoinId = "Bitcoin",
                Quantity = 0.5m,
                UnitPrice = 40000.25m,
                Fee = 0m,
                OccurredAt = Moment,
            },
        });
    }

    [Theory]
    [InlineData("12.5")]
    [InlineData("0")]
    [InlineData("0.00000001")]
    public async Task Should_AddTransactionWithFee_When_FeeIsGiven(string rawFee)
    {
        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "tx", "add", "3", "sell", "bitcoin", "0.5", "40000", "2024-01-31T14:30:00Z", "--fee", rawFee],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _portfolios.CapturedAddTransactionCommand!.Transaction.Fee.Should().Be(decimal.Parse(rawFee, CultureInfo.InvariantCulture));
        _portfolios.CapturedAddTransactionCommand.Transaction.Kind.Should().Be(TransactionKind.Sell);
    }

    [Fact]
    public async Task Should_AcceptFeeBeforeThePositionalArguments_When_FeeIsGivenFirst()
    {
        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "--fee", "2", "tx", "add", "3", "buy", "bitcoin", "0.5", "40000", "2024-01-31T14:30:00Z"],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _portfolios.CapturedAddTransactionCommand!.Transaction.Fee.Should().Be(2m);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("1,5")]
    [InlineData("1.2.3")]
    [InlineData("1e3")]
    [InlineData(" 1")]
    [InlineData("1,000")]
    [InlineData("NaN")]
    public async Task Should_ReturnOneAndNumberError_When_FeeIsNotANumber(string rawFee)
    {
        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "tx", "add", "3", "buy", "bitcoin", "0.5", "40000", "2024-01-31T14:30:00Z", "--fee", rawFee],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Fee must be a number.\n");
        _output.ToString().Should().BeEmpty();
        _portfolios.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("-0.00000001")]
    public async Task Should_ReturnOneAndNonNegativeError_When_FeeIsNegative(string rawFee)
    {
        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "tx", "add", "3", "buy", "bitcoin", "0.5", "40000", "2024-01-31T14:30:00Z", "--fee", rawFee],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Fee must be zero or greater.\n");
        _portfolios.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("0,5")]
    [InlineData("1,000")]
    public async Task Should_ReturnOneAndNumberError_When_QuantityIsNotANumber(string rawQuantity)
    {
        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "tx", "add", "3", "buy", "bitcoin", rawQuantity, "40000", "2024-01-31T14:30:00Z"],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Quantity must be a number.\n");
        _portfolios.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.5")]
    public async Task Should_ReturnOneAndPositiveError_When_QuantityIsNotPositive(string rawQuantity)
    {
        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "tx", "add", "3", "buy", "bitcoin", rawQuantity, "40000", "2024-01-31T14:30:00Z"],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Quantity must be greater than zero.\n");
        _portfolios.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("abc", "Price must be a number.")]
    [InlineData("", "Price must be a number.")]
    [InlineData("40,000", "Price must be a number.")]
    [InlineData("-1", "Price must be zero or greater.")]
    public async Task Should_ReturnOneAndValidationError_When_PriceIsInvalid(string rawPrice, string expectedError)
    {
        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "tx", "add", "3", "buy", "bitcoin", "0.5", rawPrice, "2024-01-31T14:30:00Z"],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"{expectedError}\n");
        _portfolios.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_AcceptZeroPrice_When_PriceIsZero()
    {
        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "tx", "add", "3", "buy", "bitcoin", "0.5", "0", "2024-01-31T14:30:00Z"],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _portfolios.CapturedAddTransactionCommand!.Transaction.UnitPrice.Should().Be(0m);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hold")]
    [InlineData("1")]
    [InlineData(" buy")]
    public async Task Should_ReturnOneAndValidationError_When_KindIsNotAToken(string rawKind)
    {
        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "tx", "add", "3", rawKind, "bitcoin", "0.5", "40000", "2024-01-31T14:30:00Z"],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Kind must be 'buy' or 'sell'.\n");
        _portfolios.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("BUY", TransactionKind.Buy)]
    [InlineData("Sell", TransactionKind.Sell)]
    public async Task Should_AcceptKindInAnyCase_When_KindIsABuyOrSellToken(string rawKind, TransactionKind expected)
    {
        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "tx", "add", "3", rawKind, "bitcoin", "0.5", "40000", "2024-01-31T14:30:00Z"],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _portfolios.CapturedAddTransactionCommand!.Transaction.Kind.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("yesterday")]
    [InlineData("2024-01-31")]
    [InlineData("31-01-2024")]
    [InlineData("01/31/2024 14:30:00")]
    [InlineData("2024-01-31T14:30:00")]
    [InlineData("2024-01-31 14:30:00Z")]
    [InlineData("2024-13-31T14:30:00Z")]
    public async Task Should_ReturnOneAndTimestampError_When_WhenIsNotAnIso8601Timestamp(string rawWhen)
    {
        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "tx", "add", "3", "buy", "bitcoin", "0.5", "40000", rawWhen],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be(
            "When must be an ISO 8601 timestamp with a UTC offset, such as 2024-01-31T14:30:00Z.\n");
        _portfolios.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("2024-01-31T14:30:00Z")]
    [InlineData("2024-01-31T16:30:00+02:00")]
    [InlineData("2024-01-31T09:30:00-05:00")]
    [InlineData("2024-01-31T14:30:00.0000000Z")]
    public async Task Should_ConvertWhenToUtc_When_WhenCarriesAnOffset(string rawWhen)
    {
        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "tx", "add", "3", "buy", "bitcoin", "0.5", "40000", rawWhen],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _portfolios.CapturedAddTransactionCommand!.Transaction.OccurredAt.Should().Be(Moment);
        _portfolios.CapturedAddTransactionCommand.Transaction.OccurredAt.Offset.Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Should_ReturnOneAndValidationError_When_CoinIsBlank(string coin)
    {
        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "tx", "add", "3", "buy", coin, "0.5", "40000", "2024-01-31T14:30:00Z"],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Coin is required.\n");
        _portfolios.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("abc", "Id must be a whole number.")]
    [InlineData("1.5", "Id must be a whole number.")]
    [InlineData("0", "Id must be a positive whole number.")]
    [InlineData("-3", "Id must be a positive whole number.")]
    public async Task Should_ReturnOneAndValidationError_When_AddTransactionIdIsInvalid(string rawId, string expectedError)
    {
        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "tx", "add", rawId, "buy", "bitcoin", "0.5", "40000", "2024-01-31T14:30:00Z"],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"{expectedError}\n");
        _portfolios.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ParseDecimalPointNumbersInvariantly_When_CurrentCultureIsGerman()
    {
        // Arrange
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        try
        {
            // Act
            var exitCode = await _runner.RunAsync(
                ["portfolio", "tx", "add", "3", "buy", "bitcoin", "0.5", "40000.25", "2024-01-31T14:30:00Z", "--fee", "1.5"],
                CancellationToken.None);

            // Assert
            exitCode.Should().Be(0);
            var transaction = _portfolios.CapturedAddTransactionCommand!.Transaction;
            transaction.Quantity.Should().Be(0.5m);
            transaction.UnitPrice.Should().Be(40000.25m);
            transaction.Fee.Should().Be(1.5m);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("0,5", "40000", "1", "Quantity must be a number.")]
    [InlineData("0.5", "40000,25", "1", "Price must be a number.")]
    [InlineData("0.5", "40000", "1,5", "Fee must be a number.")]
    public async Task Should_RejectCommaDecimals_When_CurrentCultureIsGerman(string rawQuantity, string rawPrice, string rawFee, string expectedError)
    {
        // Arrange
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        try
        {
            // Act
            var exitCode = await _runner.RunAsync(
                ["portfolio", "tx", "add", "3", "buy", "bitcoin", rawQuantity, rawPrice, "2024-01-31T14:30:00Z", "--fee", rawFee],
                CancellationToken.None);

            // Assert
            exitCode.Should().Be(1);
            _error.ToString().Should().Be($"{expectedError}\n");
            _portfolios.CallCount.Should().Be(0);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task Should_ReplaceTransactionAndPrintPortfolio_When_EditSucceeds()
    {
        // Arrange
        _portfolios.ReplaceTransactionResult = ServiceResult<PortfolioView>.Success(new PortfolioView
        {
            Id = 3,
            Name = "main",
            VsCurrency = "usd",
            CostBasisMethod = CostBasisMethod.Fifo,
            Version = 5,
            Transactions = [],
        });

        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "tx", "edit", "3", "9", "sell", "Ethereum", "2", "1800.5", "2024-01-31T14:30:00Z", "--fee", "0.25"],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().StartWith("Id: 3\nName: main\n").And.Contain("Version: 5\n");
        _portfolios.CapturedReplaceTransactionCommand.Should().Be(new ReplaceTransactionCommand
        {
            PortfolioId = 3,
            TransactionId = 9,
            Transaction = new TransactionDetails
            {
                Kind = TransactionKind.Sell,
                CoinId = "Ethereum",
                Quantity = 2m,
                UnitPrice = 1800.5m,
                Fee = 0.25m,
                OccurredAt = Moment,
            },
        });
    }

    [Fact]
    public async Task Should_RemoveTransactionAndPrintPortfolio_When_TransactionDeleteSucceeds()
    {
        // Arrange
        _portfolios.RemoveTransactionResult = ServiceResult<PortfolioView>.Success(new PortfolioView
        {
            Id = 3,
            Name = "main",
            VsCurrency = "usd",
            CostBasisMethod = CostBasisMethod.AverageCost,
            Version = 6,
            Transactions = [],
        });

        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "tx", "delete", "3", "9"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().StartWith("Id: 3\nName: main\n").And.Contain("Transactions: none\n");
        _portfolios.CapturedRemoveTransactionCommand.Should().Be(new RemoveTransactionCommand { PortfolioId = 3, TransactionId = 9 });
    }

    [Theory]
    [InlineData("list")]
    [InlineData("create", "main", "usd", "fifo")]
    [InlineData("show", "1")]
    [InlineData("update", "1", "main", "fifo")]
    [InlineData("delete", "1")]
    [InlineData("value", "1")]
    [InlineData("tx", "delete", "1", "2")]
    public async Task Should_ReturnOneAndUsageWithoutCallingService_When_SubcommandIsGivenFeeOption(params string[] subcommand)
    {
        // Act
        var exitCode = await _runner.RunAsync(["portfolio", .. subcommand, "--fee", "1"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Contain("Usage:");
        _output.ToString().Should().BeEmpty();
        _portfolios.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReplaceWithZeroFee_When_EditHasNoFeeOption()
    {
        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "tx", "edit", "3", "9", "buy", "bitcoin", "1", "100", "2024-01-31T14:30:00Z"],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _portfolios.CapturedReplaceTransactionCommand!.Transaction.Fee.Should().Be(0m);
    }

    [Theory]
    [InlineData("abc", "9", "Id must be a whole number.")]
    [InlineData("0", "9", "Id must be a positive whole number.")]
    [InlineData("3", "abc", "Transaction id must be a whole number.")]
    [InlineData("3", "1.5", "Transaction id must be a whole number.")]
    [InlineData("3", "0", "Transaction id must be a positive whole number.")]
    [InlineData("3", "-1", "Transaction id must be a positive whole number.")]
    public async Task Should_ReturnOneAndValidationError_When_EditIdsAreInvalid(string rawId, string rawTransactionId, string expectedError)
    {
        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "tx", "edit", rawId, rawTransactionId, "buy", "bitcoin", "1", "100", "2024-01-31T14:30:00Z"],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"{expectedError}\n");
        _portfolios.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("hold", "bitcoin", "1", "100", "2024-01-31T14:30:00Z", "Kind must be 'buy' or 'sell'.")]
    [InlineData("buy", " ", "1", "100", "2024-01-31T14:30:00Z", "Coin is required.")]
    [InlineData("buy", "bitcoin", "0", "100", "2024-01-31T14:30:00Z", "Quantity must be greater than zero.")]
    [InlineData("buy", "bitcoin", "1,5", "100", "2024-01-31T14:30:00Z", "Quantity must be a number.")]
    [InlineData("buy", "bitcoin", "1", "-1", "2024-01-31T14:30:00Z", "Price must be zero or greater.")]
    [InlineData("buy", "bitcoin", "1", "100", "2024-01-31", "When must be an ISO 8601 timestamp with a UTC offset, such as 2024-01-31T14:30:00Z.")]
    public async Task Should_ReturnOneAndValidationError_When_EditDetailsAreInvalid(
        string kind, string coin, string quantity, string price, string when, string expectedError)
    {
        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "tx", "edit", "3", "9", kind, coin, quantity, price, when],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"{expectedError}\n");
        _portfolios.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("abc", "9", "Id must be a whole number.")]
    [InlineData("0", "9", "Id must be a positive whole number.")]
    [InlineData("3", "abc", "Transaction id must be a whole number.")]
    [InlineData("3", "0", "Transaction id must be a positive whole number.")]
    [InlineData("3", "-1", "Transaction id must be a positive whole number.")]
    public async Task Should_ReturnOneAndValidationError_When_TransactionDeleteIdsAreInvalid(string rawId, string rawTransactionId, string expectedError)
    {
        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "tx", "delete", rawId, rawTransactionId], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"{expectedError}\n");
        _portfolios.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_PrintPortfolioJson_When_TransactionWriteSucceedsWithJsonFlag()
    {
        // Act
        var exitCode = await _runner.RunAsync(
            ["portfolio", "tx", "add", "3", "buy", "bitcoin", "0.5", "40000", "2024-01-31T14:30:00Z", "--fee", "1", "--json"],
            CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Contain("\"Name\": \"main\"").And.Contain("\"CostBasisMethod\": \"average-cost\"");
        _portfolios.CapturedAddTransactionCommand!.Transaction.Fee.Should().Be(1m);
    }
}
