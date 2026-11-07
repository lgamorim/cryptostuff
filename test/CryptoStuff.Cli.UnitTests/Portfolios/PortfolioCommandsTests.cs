using System.Globalization;
using CryptoStuff.Cli.UnitTests.TestSupport;
using CryptoStuff.Core;
using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Cli.UnitTests.Portfolios;

public class PortfolioCommandsTests
{
    private readonly SpyCryptocurrencyService _market = new();
    private readonly SpyPortfolioService _portfolios = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private readonly CliRunner _runner;

    public PortfolioCommandsTests()
    {
        _runner = new CliRunner(_market, _output, _error, portfolios: () => _portfolios);
    }

    [Fact]
    public async Task Should_ReturnZeroAndListText_When_ListSucceeds()
    {
        // Arrange
        _portfolios.ListResult = ServiceResult<IReadOnlyList<PortfolioView>>.Success(
        [
            new PortfolioView
            {
                Id = 1,
                Name = "main",
                VsCurrency = "usd",
                CostBasisMethod = CostBasisMethod.AverageCost,
                Version = 3,
                Transactions = [],
            },
            new PortfolioView
            {
                Id = 2,
                Name = "long term",
                VsCurrency = "eur",
                CostBasisMethod = CostBasisMethod.Fifo,
                Version = 1,
                Transactions = [Transaction(id: 7), Transaction(id: 8)],
            },
        ]);

        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "list"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be("1: main (usd, average-cost, 0 transactions)\n2: long term (eur, fifo, 2 transactions)\n");
        _error.ToString().Should().BeEmpty();
    }

    private static PortfolioTransactionView Transaction(int id) => new()
    {
        Id = id,
        Sequence = id,
        Kind = TransactionKind.Buy,
        CoinId = "bitcoin",
        Quantity = 0.5m,
        UnitPrice = 40000m,
        Fee = 10m,
        OccurredAt = new DateTimeOffset(2024, 1, 31, 14, 30, 0, TimeSpan.Zero),
    };

    [Fact]
    public async Task Should_ReturnZeroAndJsonWithKebabCaseTokens_When_ListSucceedsWithJsonFlag()
    {
        // Arrange
        _portfolios.ListResult = ServiceResult<IReadOnlyList<PortfolioView>>.Success(
        [
            new PortfolioView
            {
                Id = 2,
                Name = "long term",
                VsCurrency = "eur",
                CostBasisMethod = CostBasisMethod.AverageCost,
                Version = 1,
                Transactions = [Transaction(id: 7)],
            },
        ]);

        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "list", "--json"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should()
            .Contain("\"Name\": \"long term\"")
            .And.Contain("\"CostBasisMethod\": \"average-cost\"")
            .And.Contain("\"Kind\": \"buy\"");
    }

    [Fact]
    public async Task Should_PrintPlaceholder_When_ListIsEmpty()
    {
        // Arrange
        _portfolios.ListResult = ServiceResult<IReadOnlyList<PortfolioView>>.Success([]);

        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "list"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be("(no portfolios)\n");
    }

    [Fact]
    public async Task Should_UseSingularTransaction_When_ListedPortfolioHasOne()
    {
        // Arrange
        _portfolios.ListResult = ServiceResult<IReadOnlyList<PortfolioView>>.Success(
        [
            new PortfolioView
            {
                Id = 2,
                Name = "long term",
                VsCurrency = "eur",
                CostBasisMethod = CostBasisMethod.Fifo,
                Version = 1,
                Transactions = [Transaction(id: 7)],
            },
        ]);

        // Act
        await _runner.RunAsync(["portfolio", "list"], CancellationToken.None);

        // Assert
        _output.ToString().Should().Be("2: long term (eur, fifo, 1 transaction)\n");
    }

    [Fact]
    public async Task Should_PrintPortfolioTextAndForwardId_When_ShowSucceeds()
    {
        // Arrange
        _portfolios.GetResult = ServiceResult<PortfolioView>.Success(new PortfolioView
        {
            Id = 7,
            Name = "main",
            VsCurrency = "usd",
            CostBasisMethod = CostBasisMethod.Fifo,
            Version = 3,
            Transactions = [Transaction(id: 12)],
        });

        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "show", "7"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be(
            "Id: 7\n"
            + "Name: main\n"
            + "Currency: usd\n"
            + "Method: fifo\n"
            + "Version: 3\n"
            + "Transactions:\n"
            + "  12: 2024-01-31T14:30:00Z buy 0.5 bitcoin @ 40000 usd, fee 10 usd\n");
        _portfolios.CapturedId.Should().Be(7);
    }

    [Fact]
    public async Task Should_PrintNoTransactionsPlaceholder_When_ShownPortfolioHasNone()
    {
        // Arrange
        // The spy's default portfolio has no transactions.

        // Act
        await _runner.RunAsync(["portfolio", "show", "1"], CancellationToken.None);

        // Assert
        _output.ToString().Should().Be(
            "Id: 1\nName: main\nCurrency: usd\nMethod: average-cost\nVersion: 1\nTransactions: none\n");
    }

    [Fact]
    public async Task Should_PrintPortfolioJsonWithTransactions_When_ShowSucceedsWithJsonFlag()
    {
        // Arrange
        _portfolios.GetResult = ServiceResult<PortfolioView>.Success(new PortfolioView
        {
            Id = 7,
            Name = "main",
            VsCurrency = "usd",
            CostBasisMethod = CostBasisMethod.Fifo,
            Version = 3,
            Transactions = [Transaction(id: 12) with { Kind = TransactionKind.Sell }],
        });

        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "show", "7", "--json"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should()
            .Contain("\"CostBasisMethod\": \"fifo\"")
            .And.Contain("\"Kind\": \"sell\"")
            .And.Contain("\"Quantity\": 0.5")
            .And.Contain("\"OccurredAt\": \"2024-01-31T14:30:00Z\"");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("1,5")]
    [InlineData("")]
    [InlineData("99999999999")]
    public async Task Should_ReturnOneAndWholeNumberError_When_ShowIdIsNotAWholeNumber(string rawId)
    {
        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "show", rawId], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Id must be a whole number.\n");
        _output.ToString().Should().BeEmpty();
        _portfolios.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    public async Task Should_ReturnOneAndPositiveError_When_ShowIdIsNotPositive(string rawId)
    {
        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "show", rawId], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Id must be a positive whole number.\n");
        _portfolios.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("average-cost", CostBasisMethod.AverageCost)]
    [InlineData("fifo", CostBasisMethod.Fifo)]
    [InlineData("FIFO", CostBasisMethod.Fifo)]
    public async Task Should_CreateAndPrintPortfolio_When_CreateSucceeds(string token, CostBasisMethod expected)
    {
        // Arrange
        _portfolios.CreateResult = ServiceResult<PortfolioView>.Success(new PortfolioView
        {
            Id = 4,
            Name = "main",
            VsCurrency = "usd",
            CostBasisMethod = expected,
            Version = 1,
            Transactions = [],
        });

        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "create", "main", "USD", token], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().StartWith("Id: 4\nName: main\nCurrency: usd\n");
        _portfolios.CapturedCreateCommand.Should().Be(
            new CreatePortfolioCommand { Name = "main", VsCurrency = "USD", CostBasisMethod = expected });
    }

    [Theory]
    [InlineData("", "Name is required.")]
    [InlineData("   ", "Name is required.")]
    [InlineData("this name is far too long for a portfolio because it goes on and on and on and on and on and on and on", "Name must be at most 100 characters.")]
    public async Task Should_ReturnOneAndValidationError_When_CreateNameIsInvalid(string name, string expectedError)
    {
        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "create", name, "usd", "fifo"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"{expectedError}\n");
        _portfolios.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Should_ReturnOneAndValidationError_When_CreateCurrencyIsBlank(string currency)
    {
        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "create", "main", currency, "fifo"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Currency is required.\n");
        _portfolios.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("average")]
    [InlineData("AverageCost")]
    [InlineData("0")]
    [InlineData(" fifo")]
    public async Task Should_ReturnOneAndValidationError_When_CreateMethodIsNotAToken(string rawMethod)
    {
        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "create", "main", "usd", rawMethod], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Method must be 'average-cost' or 'fifo'.\n");
        _portfolios.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_UpdateAndPrintPortfolio_When_UpdateSucceeds()
    {
        // Arrange
        _portfolios.UpdateResult = ServiceResult<PortfolioView>.Success(new PortfolioView
        {
            Id = 4,
            Name = "renamed",
            VsCurrency = "usd",
            CostBasisMethod = CostBasisMethod.Fifo,
            Version = 2,
            Transactions = [],
        });

        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "update", "4", "renamed", "fifo"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().StartWith("Id: 4\nName: renamed\n");
        _portfolios.CapturedUpdateCommand.Should().Be(
            new UpdatePortfolioCommand { PortfolioId = 4, Name = "renamed", CostBasisMethod = CostBasisMethod.Fifo });
    }

    [Theory]
    [InlineData("abc", "Id must be a whole number.")]
    [InlineData("1.5", "Id must be a whole number.")]
    [InlineData("0", "Id must be a positive whole number.")]
    [InlineData("-2", "Id must be a positive whole number.")]
    public async Task Should_ReturnOneAndValidationError_When_UpdateIdIsInvalid(string rawId, string expectedError)
    {
        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "update", rawId, "renamed", "fifo"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"{expectedError}\n");
        _portfolios.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("", "Name is required.")]
    [InlineData("  ", "Name is required.")]
    [InlineData("this name is far too long for a portfolio because it goes on and on and on and on and on and on and on", "Name must be at most 100 characters.")]
    public async Task Should_ReturnOneAndValidationError_When_UpdateNameIsInvalid(string name, string expectedError)
    {
        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "update", "1", name, "fifo"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"{expectedError}\n");
        _portfolios.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("simple")]
    [InlineData("Fifo ")]
    public async Task Should_ReturnOneAndValidationError_When_UpdateMethodIsNotAToken(string rawMethod)
    {
        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "update", "1", "renamed", rawMethod], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Method must be 'average-cost' or 'fifo'.\n");
        _portfolios.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_DeleteAndConfirm_When_DeleteSucceeds()
    {
        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "delete", "5"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be("Deleted portfolio 5.\n");
        _portfolios.CapturedId.Should().Be(5);
    }

    [Fact]
    public async Task Should_PrintDeletedJson_When_DeleteSucceedsWithJsonFlag()
    {
        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "delete", "5", "--json"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Contain("\"Id\": 5").And.Contain("\"Deleted\": true");
    }

    [Fact]
    public async Task Should_PrintValuationTextAndForwardId_When_ValueSucceeds()
    {
        // Arrange
        _portfolios.ValuationResult = ServiceResult<PortfolioValuationView>.Success(new PortfolioValuationView
        {
            PortfolioId = 3,
            VsCurrency = "usd",
            CostBasisMethod = CostBasisMethod.Fifo,
            Coins =
            [
                new CoinValuationView
                {
                    CoinId = "bitcoin",
                    Quantity = 0.5m,
                    MarketValue = 21000.5m,
                    CostBasis = 20000m,
                    UnrealizedProfitAndLoss = 1000.5m,
                    RealizedProfitAndLoss = 0m,
                },
                new CoinValuationView
                {
                    CoinId = "ethereum",
                    Quantity = 0m,
                    MarketValue = 0m,
                    CostBasis = 0m,
                    UnrealizedProfitAndLoss = 0m,
                    RealizedProfitAndLoss = -25.25m,
                },
            ],
            Totals = new PortfolioValuationTotals
            {
                MarketValue = 21000.5m,
                CostBasis = 20000m,
                UnrealizedProfitAndLoss = 1000.5m,
                RealizedProfitAndLoss = -25.25m,
            },
        });

        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "value", "3"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be(
            "Portfolio: 3 (usd, fifo)\n"
            + "bitcoin: quantity 0.5, market value 21000.5, cost basis 20000, unrealized 1000.5, realized 0\n"
            + "ethereum: quantity 0, market value 0, cost basis 0, unrealized 0, realized -25.25\n"
            + "Total: market value 21000.5, cost basis 20000, unrealized 1000.5, realized -25.25\n");
        _portfolios.CapturedId.Should().Be(3);
    }

    [Fact]
    public async Task Should_PrintNoHoldingsPlaceholder_When_ValuedPortfolioHasNoCoins()
    {
        // Arrange
        // The spy's default valuation has no coins.

        // Act
        await _runner.RunAsync(["portfolio", "value", "1"], CancellationToken.None);

        // Assert
        _output.ToString().Should().Be(
            "Portfolio: 1 (usd, average-cost)\n"
            + "(no holdings)\n"
            + "Total: market value 0, cost basis 0, unrealized 0, realized 0\n");
    }

    [Fact]
    public async Task Should_PrintValuationJson_When_ValueSucceedsWithJsonFlag()
    {
        // Arrange
        _portfolios.ValuationResult = ServiceResult<PortfolioValuationView>.Success(new PortfolioValuationView
        {
            PortfolioId = 3,
            VsCurrency = "usd",
            CostBasisMethod = CostBasisMethod.Fifo,
            Coins =
            [
                new CoinValuationView
                {
                    CoinId = "bitcoin",
                    Quantity = 0.5m,
                    MarketValue = 21000.5m,
                    CostBasis = 20000m,
                    UnrealizedProfitAndLoss = 1000.5m,
                    RealizedProfitAndLoss = 0m,
                },
            ],
            Totals = new PortfolioValuationTotals
            {
                MarketValue = 21000.5m,
                CostBasis = 20000m,
                UnrealizedProfitAndLoss = 1000.5m,
                RealizedProfitAndLoss = 0m,
            },
        });

        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "value", "3", "--json"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should()
            .Contain("\"CostBasisMethod\": \"fifo\"")
            .And.Contain("\"CoinId\": \"bitcoin\"")
            .And.Contain("\"MarketValue\": 21000.5");
    }

    [Fact]
    public async Task Should_PrintDecimalsWithAPoint_When_CurrentCultureIsGerman()
    {
        // Arrange
        _portfolios.GetResult = ServiceResult<PortfolioView>.Success(new PortfolioView
        {
            Id = 7,
            Name = "main",
            VsCurrency = "usd",
            CostBasisMethod = CostBasisMethod.Fifo,
            Version = 3,
            Transactions = [Transaction(id: 12) with { Quantity = 1234.5m }],
        });
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        try
        {
            // Act
            await _runner.RunAsync(["portfolio", "show", "7"], CancellationToken.None);

            // Assert
            _output.ToString().Should().Contain(" buy 1234.5 bitcoin @ 40000 usd, fee 10 usd");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task Should_PrintValuationFiguresWithAPoint_When_CurrentCultureIsGerman()
    {
        // Arrange
        _portfolios.ValuationResult = ServiceResult<PortfolioValuationView>.Success(new PortfolioValuationView
        {
            PortfolioId = 3,
            VsCurrency = "usd",
            CostBasisMethod = CostBasisMethod.Fifo,
            Coins = [],
            Totals = new PortfolioValuationTotals
            {
                MarketValue = 1234.5m,
                CostBasis = 1000m,
                UnrealizedProfitAndLoss = 234.5m,
                RealizedProfitAndLoss = -0.25m,
            },
        });
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        try
        {
            // Act
            await _runner.RunAsync(["portfolio", "value", "3"], CancellationToken.None);

            // Assert
            _output.ToString().Should().Contain("Total: market value 1234.5, cost basis 1000, unrealized 234.5, realized -0.25");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("show", "abc", "Id must be a whole number.")]
    [InlineData("show", "0", "Id must be a positive whole number.")]
    [InlineData("delete", "abc", "Id must be a whole number.")]
    [InlineData("delete", "0", "Id must be a positive whole number.")]
    [InlineData("value", "abc", "Id must be a whole number.")]
    [InlineData("value", "0", "Id must be a positive whole number.")]
    public async Task Should_ReturnOneAndValidationError_When_IdIsInvalid(string subcommand, string rawId, string expectedError)
    {
        // Act
        var exitCode = await _runner.RunAsync(["portfolio", subcommand, rawId], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"{expectedError}\n");
        _portfolios.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_PrintJson_When_CreateSucceedsWithJsonFlag()
    {
        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "create", "main", "usd", "fifo", "--json"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Contain("\"Name\": \"main\"").And.NotContain("Id: 1");
    }

    [Fact]
    public async Task Should_PrintJson_When_UpdateSucceedsWithJsonFlag()
    {
        // Act
        var exitCode = await _runner.RunAsync(["portfolio", "update", "1", "main", "fifo", "--json"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Contain("\"Name\": \"main\"").And.NotContain("Id: 1");
    }
}
