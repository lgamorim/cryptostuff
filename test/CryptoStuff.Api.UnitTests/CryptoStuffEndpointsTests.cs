using CryptoStuff.Api.UnitTests.TestSupport;
using CryptoStuff.Core;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CryptoStuff.Api.UnitTests;

public class CryptoStuffEndpointsTests
{
    private readonly SpyCryptocurrencyService _service = new();

    [Fact]
    public async Task Should_ReturnOkWithPrices_When_PricesRequestValid()
    {
        _service.PricesResult = ServiceResult<CoinPriceView>.Success(new CoinPriceView
        {
            Coins = [new CoinPrice { CoinId = "bitcoin", Amounts = [new CurrencyAmount { Currency = "usd", Amount = 43000m }] }],
        });

        var result = await CryptoStuffEndpoints.GetPricesAsync("bitcoin", "usd", _service, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<CoinPriceView>>().Subject;
        ok.Value.Should().BeEquivalentTo(_service.PricesResult.Value);
        _service.CapturedPricesQuery.Should().BeEquivalentTo(new CoinPriceQuery { CoinIds = ["bitcoin"], VsCurrencies = ["usd"] });
    }

    [Fact]
    public async Task Should_ParseCommaSeparatedAndTrimEntries_When_PricesRequestHasLists()
    {
        await CryptoStuffEndpoints.GetPricesAsync("bitcoin, ethereum", "usd,eur", _service, CancellationToken.None);

        _service.CapturedPricesQuery.Should().BeEquivalentTo(new CoinPriceQuery
        {
            CoinIds = ["bitcoin", "ethereum"],
            VsCurrencies = ["usd", "eur"],
        });
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_CoinsMissing()
    {
        var result = await CryptoStuffEndpoints.GetPricesAsync(null, "usd", _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Status.Should().Be(400);
        problem.ProblemDetails.Errors.Should().ContainKey("coins").WhoseValue.Should().Equal("coins is required.");
        _service.CapturedPricesQuery.Should().BeNull();
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_CoinsHasBlankEntry()
    {
        var result = await CryptoStuffEndpoints.GetPricesAsync("bitcoin,,ethereum", "usd", _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("coins").WhoseValue.Should().Equal("coins contains a blank entry.");
    }

    [Fact]
    public async Task Should_ReturnOkWithTokenPrices_When_TokenPricesRequestValid()
    {
        var result = await CryptoStuffEndpoints.GetTokenPricesAsync("ethereum", "0xabc,0xdef", "usd", _service, CancellationToken.None);

        result.Should().BeOfType<Ok<TokenPriceView>>();
        _service.CapturedTokenPricesQuery.Should().BeEquivalentTo(new TokenPriceQuery
        {
            Platform = "ethereum",
            ContractAddresses = ["0xabc", "0xdef"],
            VsCurrencies = ["usd"],
        });
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_PlatformBlank()
    {
        var result = await CryptoStuffEndpoints.GetTokenPricesAsync(" ", "0xabc", "usd", _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("platform").WhoseValue.Should().Equal("platform is required.");
    }

    [Fact]
    public async Task Should_ReturnOkWithMarketChart_When_HistoricalRequestValid()
    {
        var result = await CryptoStuffEndpoints.GetHistoricalMarketDataAsync("bitcoin", "usd", "30", _service, CancellationToken.None);

        result.Should().BeOfType<Ok<CoinMarketChartView>>();
        _service.CapturedMarketChartQuery.Should().BeEquivalentTo(new CoinMarketChartQuery { CoinId = "bitcoin", VsCurrency = "usd", Days = 30 });
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_DaysNotNumeric()
    {
        var result = await CryptoStuffEndpoints.GetHistoricalMarketDataAsync("bitcoin", "usd", "many", _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("days").WhoseValue.Should().Equal("days must be a whole number.");
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_DaysMissing()
    {
        var result = await CryptoStuffEndpoints.GetHistoricalMarketDataAsync("bitcoin", "usd", null, _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("days").WhoseValue.Should().Equal("days is required.");
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_DaysBlank()
    {
        var result = await CryptoStuffEndpoints.GetHistoricalMarketDataAsync("bitcoin", "usd", " ", _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("days").WhoseValue.Should().Equal("days is required.");
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_DaysNotPositive()
    {
        var result = await CryptoStuffEndpoints.GetHistoricalMarketDataAsync("bitcoin", "usd", "0", _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("days").WhoseValue.Should().Equal("days must be a positive number of days.");
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_CoinBlankInHistoricalRequest()
    {
        var result = await CryptoStuffEndpoints.GetHistoricalMarketDataAsync(" ", "usd", "30", _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("coin");
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_CurrencyBlankInHistoricalRequest()
    {
        var result = await CryptoStuffEndpoints.GetHistoricalMarketDataAsync("bitcoin", " ", "30", _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("currency");
    }

    [Fact]
    public async Task Should_ReturnOkWithCoin_When_CoinRequestValid()
    {
        var result = await CryptoStuffEndpoints.GetCoinAsync("bitcoin", _service, CancellationToken.None);

        result.Should().BeOfType<Ok<CoinView>>();
        _service.CapturedCoinQuery.Should().BeEquivalentTo(new CoinQuery { CoinId = "bitcoin" });
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_CoinIdBlank()
    {
        var result = await CryptoStuffEndpoints.GetCoinAsync(" ", _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("coin").WhoseValue.Should().Equal("coin is required.");
    }

    [Fact]
    public async Task Should_ReturnOkWithDeveloperData_When_DeveloperDataRequestValid()
    {
        var result = await CryptoStuffEndpoints.GetDeveloperDataAsync("bitcoin", "29-02-2024", _service, CancellationToken.None);

        result.Should().BeOfType<Ok<CoinDeveloperDataView>>();
        _service.CapturedDeveloperDataQuery.Should().BeEquivalentTo(new CoinDeveloperDataQuery { CoinId = "bitcoin", Date = "29-02-2024" });
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_DateMalformed()
    {
        var result = await CryptoStuffEndpoints.GetDeveloperDataAsync("bitcoin", "2024-02-29", _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("date").WhoseValue.Should().Equal("date must be a valid date in dd-MM-yyyy format.");
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_DateBlank()
    {
        var result = await CryptoStuffEndpoints.GetDeveloperDataAsync("bitcoin", " ", _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("date").WhoseValue.Should().Equal("date is required.");
    }

    [Theory]
    [InlineData(ServiceErrorCode.NotFound, 404, "Not Found", "The requested resource was not found.")]
    [InlineData(ServiceErrorCode.RateLimited, 429, "Too Many Requests", "The CoinGecko rate limit was exceeded. Try again later.")]
    [InlineData(ServiceErrorCode.RequestTimedOut, 504, "Gateway Timeout", "The request to CoinGecko timed out.")]
    [InlineData(ServiceErrorCode.UpstreamUnavailable, 502, "Bad Gateway", "CoinGecko is currently unavailable.")]
    [InlineData((ServiceErrorCode)99, 500, "Internal Server Error", "An unknown error occurred.")]
    public async Task Should_ReturnMappedProblem_When_ServiceFails(
        ServiceErrorCode errorCode, int expectedStatus, string expectedTitle, string expectedDetail)
    {
        _service.CoinResult = ServiceResult<CoinView>.Failure(errorCode);

        var result = await CryptoStuffEndpoints.GetCoinAsync("bitcoin", _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.ProblemDetails.Status.Should().Be(expectedStatus);
        problem.ProblemDetails.Title.Should().Be(expectedTitle);
        problem.ProblemDetails.Detail.Should().Be(expectedDetail);
    }

    [Fact]
    public async Task Should_ForwardCancellationToken_When_Called()
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        await CryptoStuffEndpoints.GetCoinAsync("bitcoin", _service, cancellationTokenSource.Token);

        _service.CapturedCancellationToken.Should().Be(cancellationTokenSource.Token);
    }
}
