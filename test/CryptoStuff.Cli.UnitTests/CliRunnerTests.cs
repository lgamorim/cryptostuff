using CryptoStuff.Cli.UnitTests.TestSupport;
using CryptoStuff.Core;

namespace CryptoStuff.Cli.UnitTests;

public class CliRunnerTests
{
    private readonly SpyCryptocurrencyService _service = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private readonly CliRunner _runner;

    public CliRunnerTests()
    {
        _runner = new CliRunner(_service, _output, _error);
    }

    [Fact]
    public async Task Should_ReturnZeroAndPricesText_When_PriceCommandSucceeds()
    {
        _service.PricesResult = ServiceResult<CoinPriceView>.Success(new CoinPriceView
        {
            Coins = [new CoinPrice { CoinId = "bitcoin", Amounts = [new CurrencyAmount { Currency = "usd", Amount = 43000m }] }],
        });

        var exitCode = await _runner.RunAsync(["price", "bitcoin", "usd"], CancellationToken.None);

        exitCode.Should().Be(0);
        _output.ToString().Should().Be("bitcoin: 43000 usd\n");
        _error.ToString().Should().BeEmpty();
        _service.CapturedPricesQuery.Should().BeEquivalentTo(new CoinPriceQuery { CoinIds = ["bitcoin"], VsCurrencies = ["usd"] });
    }

    [Fact]
    public async Task Should_ReturnZeroAndJsonOutput_When_PriceCommandSucceedsWithJsonFlag()
    {
        _service.PricesResult = ServiceResult<CoinPriceView>.Success(new CoinPriceView
        {
            Coins = [new CoinPrice { CoinId = "bitcoin", Amounts = [new CurrencyAmount { Currency = "usd", Amount = 43000m }] }],
        });

        var exitCode = await _runner.RunAsync(["price", "bitcoin", "usd", "--json"], CancellationToken.None);

        exitCode.Should().Be(0);
        _output.ToString().Should().Contain("\"CoinId\": \"bitcoin\"");
    }

    [Fact]
    public async Task Should_ParseCommaSeparatedCoinsAndCurrencies_When_PriceCommandGivenLists()
    {
        await _runner.RunAsync(["price", "bitcoin,ethereum", "usd,eur"], CancellationToken.None);

        _service.CapturedPricesQuery.Should().BeEquivalentTo(new CoinPriceQuery
        {
            CoinIds = ["bitcoin", "ethereum"],
            VsCurrencies = ["usd", "eur"],
        });
    }

    [Fact]
    public async Task Should_ReturnOneAndUsage_When_PriceCommandMissingArguments()
    {
        var exitCode = await _runner.RunAsync(["price", "bitcoin"], CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Contain("Usage:");
        _service.CapturedPricesQuery.Should().BeNull();
    }

    [Fact]
    public async Task Should_ReturnOneAndUsage_When_PriceCommandHasExtraArguments()
    {
        var exitCode = await _runner.RunAsync(["price", "bitcoin", "usd", "eur"], CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Contain("Usage:");
    }

    [Fact]
    public async Task Should_ReturnOneAndValidationError_When_PriceCommandHasBlankCoinInList()
    {
        var exitCode = await _runner.RunAsync(["price", "bitcoin,,ethereum", "usd"], CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Coins contains a blank entry.\n");
        _service.CapturedPricesQuery.Should().BeNull();
    }

    [Fact]
    public async Task Should_TrimWhitespace_When_PriceCommandListHasPaddedEntries()
    {
        await _runner.RunAsync(["price", "bitcoin, ethereum", "usd"], CancellationToken.None);

        _service.CapturedPricesQuery.Should().BeEquivalentTo(new CoinPriceQuery
        {
            CoinIds = ["bitcoin", "ethereum"],
            VsCurrencies = ["usd"],
        });
    }

    [Fact]
    public async Task Should_ReturnZero_When_TokenCommandSucceeds()
    {
        var exitCode = await _runner.RunAsync(["token", "ethereum", "0xabc,0xdef", "usd"], CancellationToken.None);

        exitCode.Should().Be(0);
        _service.CapturedTokenPricesQuery.Should().BeEquivalentTo(new TokenPriceQuery
        {
            Platform = "ethereum",
            ContractAddresses = ["0xabc", "0xdef"],
            VsCurrencies = ["usd"],
        });
    }

    [Fact]
    public async Task Should_ReturnZeroAndJsonOutput_When_TokenCommandSucceedsWithJsonFlag()
    {
        var exitCode = await _runner.RunAsync(["token", "ethereum", "0xabc", "usd", "--json"], CancellationToken.None);

        exitCode.Should().Be(0);
        _output.ToString().TrimStart().Should().StartWith("{");
    }

    [Fact]
    public async Task Should_ReturnOneAndUsage_When_TokenCommandMissingArguments()
    {
        var exitCode = await _runner.RunAsync(["token", "ethereum", "0xabc"], CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Contain("Usage:");
    }

    [Fact]
    public async Task Should_ReturnOneAndValidationError_When_TokenCommandHasBlankPlatform()
    {
        var exitCode = await _runner.RunAsync(["token", " ", "0xabc", "usd"], CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Platform is required.\n");
    }

    [Fact]
    public async Task Should_ReturnZero_When_HistoryCommandSucceeds()
    {
        var exitCode = await _runner.RunAsync(["history", "bitcoin", "usd", "30"], CancellationToken.None);

        exitCode.Should().Be(0);
        _service.CapturedMarketChartQuery.Should().BeEquivalentTo(new CoinMarketChartQuery
        {
            CoinId = "bitcoin",
            VsCurrency = "usd",
            Days = 30,
        });
    }

    [Fact]
    public async Task Should_ReturnZeroAndJsonOutput_When_HistoryCommandSucceedsWithJsonFlag()
    {
        var exitCode = await _runner.RunAsync(["history", "bitcoin", "usd", "30", "--json"], CancellationToken.None);

        exitCode.Should().Be(0);
        _output.ToString().TrimStart().Should().StartWith("{");
    }

    [Fact]
    public async Task Should_ReturnOneAndUsage_When_HistoryCommandMissingArguments()
    {
        var exitCode = await _runner.RunAsync(["history", "bitcoin", "usd"], CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Contain("Usage:");
    }

    [Fact]
    public async Task Should_ReturnOneAndValidationError_When_HistoryCommandCoinBlank()
    {
        var exitCode = await _runner.RunAsync(["history", " ", "usd", "30"], CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Coin is required.\n");
    }

    [Fact]
    public async Task Should_ReturnOneAndValidationError_When_HistoryCommandCurrencyBlank()
    {
        var exitCode = await _runner.RunAsync(["history", "bitcoin", " ", "30"], CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Currency is required.\n");
    }

    [Fact]
    public async Task Should_ReturnOneAndValidationError_When_HistoryCommandDaysNotNumeric()
    {
        var exitCode = await _runner.RunAsync(["history", "bitcoin", "usd", "many"], CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Days must be a whole number.\n");
    }

    [Fact]
    public async Task Should_ReturnOneAndValidationError_When_HistoryCommandDaysNotPositive()
    {
        var exitCode = await _runner.RunAsync(["history", "bitcoin", "usd", "0"], CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Days must be a positive number of days.\n");
    }

    [Fact]
    public async Task Should_ReturnZero_When_CoinCommandSucceeds()
    {
        var exitCode = await _runner.RunAsync(["coin", "bitcoin"], CancellationToken.None);

        exitCode.Should().Be(0);
        _service.CapturedCoinQuery.Should().BeEquivalentTo(new CoinQuery { CoinId = "bitcoin" });
    }

    [Fact]
    public async Task Should_ReturnZeroAndJsonOutput_When_CoinCommandSucceedsWithJsonFlag()
    {
        var exitCode = await _runner.RunAsync(["coin", "bitcoin", "--json"], CancellationToken.None);

        exitCode.Should().Be(0);
        _output.ToString().TrimStart().Should().StartWith("{");
    }

    [Fact]
    public async Task Should_ReturnOneAndUsage_When_CoinCommandMissingArgument()
    {
        var exitCode = await _runner.RunAsync(["coin"], CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Contain("Usage:");
    }

    [Fact]
    public async Task Should_ReturnOneAndValidationError_When_CoinCommandIdBlank()
    {
        var exitCode = await _runner.RunAsync(["coin", " "], CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Id is required.\n");
    }

    [Fact]
    public async Task Should_ReturnZero_When_DeveloperCommandSucceeds()
    {
        var exitCode = await _runner.RunAsync(["developer", "bitcoin", "29-02-2024"], CancellationToken.None);

        exitCode.Should().Be(0);
        _service.CapturedDeveloperDataQuery.Should().BeEquivalentTo(new CoinDeveloperDataQuery { CoinId = "bitcoin", Date = "29-02-2024" });
    }

    [Fact]
    public async Task Should_ReturnZeroAndJsonOutput_When_DeveloperCommandSucceedsWithJsonFlag()
    {
        var exitCode = await _runner.RunAsync(["developer", "bitcoin", "29-02-2024", "--json"], CancellationToken.None);

        exitCode.Should().Be(0);
        _output.ToString().TrimStart().Should().StartWith("{");
    }

    [Fact]
    public async Task Should_ReturnOneAndUsage_When_DeveloperCommandMissingArguments()
    {
        var exitCode = await _runner.RunAsync(["developer", "bitcoin"], CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Contain("Usage:");
    }

    [Fact]
    public async Task Should_ReturnOneAndValidationError_When_DeveloperCommandIdBlank()
    {
        var exitCode = await _runner.RunAsync(["developer", " ", "29-02-2024"], CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Id is required.\n");
    }

    [Fact]
    public async Task Should_ReturnOneAndValidationError_When_DeveloperCommandDateMalformed()
    {
        var exitCode = await _runner.RunAsync(["developer", "bitcoin", "2024-02-29"], CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Date must be a valid date in dd-MM-yyyy format.\n");
    }

    [Fact]
    public async Task Should_ReturnOneAndValidationError_When_DeveloperCommandDateBlank()
    {
        var exitCode = await _runner.RunAsync(["developer", "bitcoin", " "], CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Date is required.\n");
    }

    [Fact]
    public async Task Should_ReturnOneAndUsage_When_NoCommandGiven()
    {
        var exitCode = await _runner.RunAsync([], CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Contain("Usage:");
    }

    [Fact]
    public async Task Should_ReturnOneAndUsage_When_UnknownCommandGiven()
    {
        var exitCode = await _runner.RunAsync(["frobnicate"], CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Contain("Usage:");
    }

    [Theory]
    [InlineData(ServiceErrorCode.NotFound, "Error: the requested resource was not found.\n")]
    [InlineData(ServiceErrorCode.RateLimited, "Error: the CoinGecko rate limit was exceeded. Try again later.\n")]
    [InlineData(ServiceErrorCode.RequestTimedOut, "Error: the request to CoinGecko timed out.\n")]
    [InlineData(ServiceErrorCode.UpstreamUnavailable, "Error: CoinGecko is currently unavailable.\n")]
    public async Task Should_ReturnOneAndMappedMessage_When_ServiceFails(ServiceErrorCode errorCode, string expectedMessage)
    {
        _service.CoinResult = ServiceResult<CoinView>.Failure(errorCode);

        var exitCode = await _runner.RunAsync(["coin", "bitcoin"], CancellationToken.None);

        exitCode.Should().Be(1);
        _error.ToString().Should().Be(expectedMessage);
        _output.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnCancellationExitCode_When_TokenIsCanceled()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        _service.ExceptionToThrow = new OperationCanceledException(cancellationTokenSource.Token);

        var exitCode = await _runner.RunAsync(["coin", "bitcoin"], cancellationTokenSource.Token);

        exitCode.Should().Be(130);
        _service.CapturedCancellationToken.Should().Be(cancellationTokenSource.Token);
        _service.CapturedCancellationToken.IsCancellationRequested.Should().BeTrue();
        _output.ToString().Should().BeEmpty();
        _error.ToString().Should().BeEmpty();
    }
}
