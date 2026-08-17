using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CryptoStuff.Api.IntegrationTests.TestSupport;
using CryptoStuff.CoinGecko;
using CryptoStuff.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace CryptoStuff.Api.IntegrationTests;

/// <summary>The upstream CoinGecko failure shapes exercised by <see cref="CryptoStuffApiTests.Should_ReturnMappedStatus_When_CoinGeckoFails"/>.</summary>
public enum CoinGeckoFailureKind
{
    RateLimited,
    Unavailable,
    Timeout,
}

public class CryptoStuffApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Should_ReturnOkWithPrices_When_PricesRequestValid()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(
            new Dictionary<string, Dictionary<string, decimal>> { ["bitcoin"] = new() { ["usd"] = 43000m } },
            HttpStatusCode.OK);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/prices?coins=bitcoin&currencies=usd", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CoinPriceView>(JsonOptions, cancellationToken);
        body.Should().BeEquivalentTo(new CoinPriceView
        {
            Coins = [new CoinPrice { CoinId = "bitcoin", Amounts = [new CurrencyAmount { Currency = "usd", Amount = 43000m }] }],
        });
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_PricesRequestHasUnknownCoin()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(
            new Dictionary<string, Dictionary<string, decimal>> { ["bitcoin"] = new() { ["usd"] = 43000m } },
            HttpStatusCode.OK);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/prices?coins=bitcoin,ghost&currencies=usd", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(JsonOptions, cancellationToken);
        problem!.Title.Should().Be("Not Found");
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_PricesRequestMissingCoins()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/prices?currencies=usd", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(JsonOptions, cancellationToken);
        problem!.Errors.Should().ContainKey("coins").WhoseValue.Should().Equal("coins is required.");
    }

    [Fact]
    public async Task Should_ReturnOkWithTokenPrices_When_TokenPricesRequestValid()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.TokenPriceMatrixResponse = CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(
            new Dictionary<string, Dictionary<string, decimal>> { ["0xabc"] = new() { ["usd"] = 1.23m } },
            HttpStatusCode.OK);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/token-prices?platform=ethereum&addresses=0xabc&currencies=usd", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TokenPriceView>(JsonOptions, cancellationToken);
        body.Should().BeEquivalentTo(new TokenPriceView
        {
            Tokens = [new TokenPrice { ContractAddress = "0xabc", Amounts = [new CurrencyAmount { Currency = "usd", Amount = 1.23m }] }],
        });
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_TokenPricesRequestMissingPlatform()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/token-prices?addresses=0xabc&currencies=usd", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(JsonOptions, cancellationToken);
        problem!.Errors.Should().ContainKey("platform");
    }

    [Fact]
    public async Task Should_ReturnOkWithMarketChart_When_HistoricalRequestValid()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.MarketChartResponse = CoinGeckoResponse<CoinGeckoMarketChart>.Success(
            new CoinGeckoMarketChart
            {
                Prices = [new CoinGeckoMarketChartPoint(1700000000000, 42000m)],
                MarketCaps = [new CoinGeckoMarketChartPoint(1700000000000, 800000000000m)],
                TotalVolumes = [new CoinGeckoMarketChartPoint(1700000000000, 25000000000m)],
            },
            HttpStatusCode.OK);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/historical-market-data?coin=bitcoin&currency=usd&days=30", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CoinMarketChartView>(JsonOptions, cancellationToken);
        body!.Prices.Should().ContainSingle(point => point.Value == 42000m);
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_HistoricalRequestDaysNotPositive()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/historical-market-data?coin=bitcoin&currency=usd&days=0", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(JsonOptions, cancellationToken);
        problem!.Errors.Should().ContainKey("days").WhoseValue.Should().Equal("days must be a positive number of days.");
    }

    [Fact]
    public async Task Should_ReturnOkWithCoin_When_CoinRequestValid()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.CoinResponse = CoinGeckoResponse<CoinGeckoCoin>.Success(
            new CoinGeckoCoin
            {
                Id = "bitcoin",
                Symbol = "btc",
                Name = "Bitcoin",
                Image = new CoinGeckoImage { Large = "https://example.com/bitcoin.png" },
            },
            HttpStatusCode.OK);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/coins/bitcoin", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CoinView>(JsonOptions, cancellationToken);
        body!.Id.Should().Be("bitcoin");
        body.ImageUrl.Should().Be("https://example.com/bitcoin.png");
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_CoinRequestUnknownCoin()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.CoinResponse = CoinGeckoResponse<CoinGeckoCoin>.Failure(HttpStatusCode.NotFound);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/coins/not-a-real-coin", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(JsonOptions, cancellationToken);
        problem!.Title.Should().Be("Not Found");
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_CoinRequestIdBlank()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/coins/%20", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(JsonOptions, cancellationToken);
        problem!.Errors.Should().ContainKey("coin").WhoseValue.Should().Equal("coin is required.");
    }

    [Theory]
    [InlineData(CoinGeckoFailureKind.RateLimited, 429)]
    [InlineData(CoinGeckoFailureKind.Unavailable, 502)]
    [InlineData(CoinGeckoFailureKind.Timeout, 504)]
    public async Task Should_ReturnMappedStatus_When_CoinGeckoFails(CoinGeckoFailureKind failureKind, int expectedStatus)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.CoinResponse = failureKind switch
        {
            CoinGeckoFailureKind.RateLimited => CoinGeckoResponse<CoinGeckoCoin>.Failure(HttpStatusCode.TooManyRequests),
            CoinGeckoFailureKind.Unavailable => CoinGeckoResponse<CoinGeckoCoin>.Failure(HttpStatusCode.ServiceUnavailable),
            CoinGeckoFailureKind.Timeout => CoinGeckoResponse<CoinGeckoCoin>.Timeout(),
            _ => throw new ArgumentOutOfRangeException(nameof(failureKind)),
        };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/coins/bitcoin", cancellationToken);

        response.StatusCode.Should().Be((HttpStatusCode)expectedStatus);
    }

    [Fact]
    public async Task Should_ReturnProblemDetails_When_RouteUnmatched()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/not-a-real-route", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Should_CallCoinGeckoOnce_When_CoinRequestedTwice()
    {
        // Proves the caching decorator is actually part of the composition this
        // suite hosts for real - if AddCryptoStuff ever stopped wrapping
        // CryptocurrencyService in CachingCryptocurrencyService, this would fail
        // with CallCount == 2 even though every other test in this file would
        // still pass.
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.CoinResponse = CoinGeckoResponse<CoinGeckoCoin>.Success(
            new CoinGeckoCoin { Id = "bitcoin", Symbol = "btc", Name = "Bitcoin", Image = new CoinGeckoImage() },
            HttpStatusCode.OK);
        using var client = factory.CreateClient();

        await client.GetAsync("/coins/bitcoin", cancellationToken);
        await client.GetAsync("/coins/bitcoin", cancellationToken);

        factory.CoinGeckoClient.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_ReturnOkWithDeveloperData_When_DeveloperDataRequestValid()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.CoinHistoryResponse = CoinGeckoResponse<CoinGeckoCoinHistory>.Success(
            new CoinGeckoCoinHistory
            {
                Id = "bitcoin",
                Symbol = "btc",
                Name = "Bitcoin",
                DeveloperData = new CoinGeckoDeveloperData { Forks = 100, Stars = 200 },
            },
            HttpStatusCode.OK);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/coins/bitcoin/developer-data?date=29-02-2024", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CoinDeveloperDataView>(JsonOptions, cancellationToken);
        body!.Forks.Should().Be(100);
        body.Stars.Should().Be(200);
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_DeveloperDataRequestDateMalformed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/coins/bitcoin/developer-data?date=2024-02-29", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(JsonOptions, cancellationToken);
        problem!.Errors.Should().ContainKey("date").WhoseValue.Should().Equal("date must be a valid date in dd-MM-yyyy format.");
    }

    [Fact]
    public async Task Should_ReturnOk_When_HealthRequested()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_ReturnOk_When_OpenApiDocumentRequested()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        body.Should().Contain("/prices");
    }

    [Fact]
    public async Task Should_ReturnOk_When_ScalarRequestedInDevelopment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var baseFactory = new CryptoStuffApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/scalar/v1", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_ScalarRequestedOutsideDevelopment()
    {
        // WebApplicationFactory defaults to the Development environment, so the
        // gating this test proves needs an explicit non-Development override -
        // the bare default factory (see the test above) would pass either way.
        var cancellationToken = TestContext.Current.CancellationToken;
        using var baseFactory = new CryptoStuffApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/scalar/v1", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
