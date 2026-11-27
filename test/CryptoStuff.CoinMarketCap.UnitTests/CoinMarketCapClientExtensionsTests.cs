using System.Net;
using CryptoStuff.CoinMarketCap.UnitTests.TestSupport;

namespace CryptoStuff.CoinMarketCap.UnitTests;

public class CoinMarketCapClientExtensionsTests
{
    private const string Status = """
        "status":{"timestamp":"2024-01-31T00:00:00.000Z","error_code":0,"error_message":null,"elapsed":12,"credit_count":1}
        """;

    private Uri? _capturedUri;

    private ICoinMarketCapClient CreateClient(string json) =>
        new CoinMarketCapClient(new HttpClient(FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, json, request => _capturedUri = request.RequestUri))
        {
            BaseAddress = new Uri("https://example.test/"),
        });

    private string CapturedPathAndQuery =>
        (_capturedUri ?? throw new InvalidOperationException("Request was not captured.")).PathAndQuery;

    // v3/cryptocurrency/quotes/latest.

    [Fact]
    public async Task Should_BuildCommaJoinedPercentEncodedRequestUriSkippingInvalidSlugs_When_RequestingLatestQuotes()
    {
        var client = CreateClient("null");

        await client.GetLatestQuotesAsync(["bitcoin", "eth,ereum"], ["USD", "EUR"], CancellationToken.None);

        CapturedPathAndQuery.Should().Be("/v3/cryptocurrency/quotes/latest?slug=bitcoin,eth%2Cereum&convert=USD,EUR&skip_invalid=true");
    }

    [Fact]
    public async Task Should_BuildEmptyQueryParameters_When_NoSlugsOrCurrenciesProvided()
    {
        var client = CreateClient("null");

        await client.GetLatestQuotesAsync([], [], CancellationToken.None);

        CapturedPathAndQuery.Should().Be("/v3/cryptocurrency/quotes/latest?slug=&convert=&skip_invalid=true");
    }

    [Fact]
    public async Task Should_ReturnEachCoinWithItsQuotes_When_LatestQuotesResponseIsSuccessful()
    {
        var client = CreateClient(
            "{" + Status + """
            ,"data":[
              {"id":1,"name":"Bitcoin","symbol":"BTC","slug":"bitcoin","cmc_rank":1,"is_active":1,"last_updated":"2024-01-31T00:00:00.000Z",
               "quote":[{"id":2781,"symbol":"USD","price":43189.52,"volume_24h":21000000000.5,"market_cap":846000000000,"percent_change_24h":1.2,"last_updated":"2024-01-31T00:00:00.000Z"}]},
              {"id":1027,"name":"Ethereum","symbol":"ETH","slug":"ethereum","quote":[{"id":2781,"symbol":"USD","price":null,"volume_24h":null,"market_cap":null,"last_updated":null}]}
            ]}
            """);

        var response = await client.GetLatestQuotesAsync(["bitcoin", "ethereum"], ["USD"], CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        response.Value.Should().BeEquivalentTo(
            [
                new CoinMarketCapLatestQuote
                {
                    Id = 1,
                    Slug = "bitcoin",
                    Quote =
                    [
                        new CoinMarketCapCurrencyQuote
                        {
                            Symbol = "USD",
                            Price = 43189.52m,
                            Volume24h = 21000000000.5m,
                            MarketCap = 846000000000m,
                            LastUpdated = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero),
                        },
                    ],
                },
                new CoinMarketCapLatestQuote
                {
                    Id = 1027,
                    Slug = "ethereum",
                    Quote = [new CoinMarketCapCurrencyQuote { Symbol = "USD" }],
                },
            ],
            options => options.WithStrictOrdering());
    }

    // v2/cryptocurrency/info.

    [Fact]
    public async Task Should_BuildCommaJoinedPercentEncodedRequestUri_When_RequestingInfoBySlug()
    {
        var client = CreateClient("null");

        await client.GetInfoBySlugAsync(["bitcoin", "usd coin"], CancellationToken.None);

        CapturedPathAndQuery.Should().Be("/v2/cryptocurrency/info?slug=bitcoin,usd%20coin");
    }

    [Fact]
    public async Task Should_BuildPercentEncodedRequestUri_When_RequestingInfoByAddress()
    {
        var client = CreateClient("null");

        await client.GetInfoByAddressAsync("0xA0b8 &x", CancellationToken.None);

        CapturedPathAndQuery.Should().Be("/v2/cryptocurrency/info?address=0xA0b8%20%26x");
    }

    [Fact]
    public async Task Should_ReturnEachCoinsInfo_When_InfoBySlugResponseIsSuccessful()
    {
        var client = CreateClient(
            "{" + Status + """
            ,"data":{
              "3408":{"id":3408,"name":"USDC","symbol":"USDC","category":"token","slug":"usd-coin",
                "logo":"https://s2.coinmarketcap.com/static/img/coins/64x64/3408.png","description":"USDC is a stablecoin.",
                "platform":{"id":1027,"name":"Ethereum","symbol":"ETH","slug":"ethereum","token_address":"0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48"},
                "urls":{"website":["https://www.centre.io/usdc"]},
                "contract_address":[
                  {"contract_address":"0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48","platform":{"name":"Ethereum","coin":{"id":"1027","name":"Ethereum","symbol":"ETH","slug":"ethereum"}}},
                  {"contract_address":"EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v","platform":{"name":"Solana","coin":{"id":"5426","name":"Solana","symbol":"SOL","slug":"solana"}}}
                ]},
              "1":{"id":1,"name":"Bitcoin","symbol":"BTC","category":"coin","slug":"bitcoin","logo":null,"description":null,"platform":null,"contract_address":[]}
            }}
            """);

        var response = await client.GetInfoBySlugAsync(["usd-coin", "bitcoin"], CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        response.Value.Should().BeEquivalentTo(new Dictionary<string, CoinMarketCapCoinInfo>
        {
            ["3408"] = new()
            {
                Id = 3408,
                Name = "USDC",
                Symbol = "USDC",
                Slug = "usd-coin",
                Logo = "https://s2.coinmarketcap.com/static/img/coins/64x64/3408.png",
                Description = "USDC is a stablecoin.",
                ContractAddresses =
                [
                    new CoinMarketCapContractAddress
                    {
                        Address = "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48",
                        Platform = new CoinMarketCapContractPlatform { Coin = new CoinMarketCapPlatformCoin { Slug = "ethereum" } },
                    },
                    new CoinMarketCapContractAddress
                    {
                        Address = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v",
                        Platform = new CoinMarketCapContractPlatform { Coin = new CoinMarketCapPlatformCoin { Slug = "solana" } },
                    },
                ],
            },
            ["1"] = new() { Id = 1, Name = "Bitcoin", Symbol = "BTC", Slug = "bitcoin", ContractAddresses = [] },
        });
    }

    [Fact]
    public async Task Should_ReturnTheTokensInfoKeyedById_When_InfoByAddressResponseIsSuccessful()
    {
        var client = CreateClient(
            "{" + Status + """
            ,"data":{"3408":{"id":3408,"name":"USDC","symbol":"USDC","category":"token","slug":"usd-coin","logo":null,"description":null,
              "contract_address":[{"contract_address":"0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48","platform":{"name":"Ethereum","coin":{"id":"1027","name":"Ethereum","symbol":"ETH","slug":"ethereum"}}}]}}}
            """);

        var response = await client.GetInfoByAddressAsync("0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48", CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        response.Value.Should().BeEquivalentTo(new Dictionary<string, CoinMarketCapCoinInfo>
        {
            ["3408"] = new()
            {
                Id = 3408,
                Name = "USDC",
                Symbol = "USDC",
                Slug = "usd-coin",
                ContractAddresses =
                [
                    new CoinMarketCapContractAddress
                    {
                        Address = "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48",
                        Platform = new CoinMarketCapContractPlatform { Coin = new CoinMarketCapPlatformCoin { Slug = "ethereum" } },
                    },
                ],
            },
        });
    }

    // v3/cryptocurrency/quotes/historical.

    [Fact]
    public async Task Should_BuildDailyRequestUriWithUnixTimesAndADayCount_When_RequestingHistoricalQuotes()
    {
        var client = CreateClient("null");

        await client.GetHistoricalQuotesAsync(
            1,
            "USD",
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 1, 31, 1, 0, 0, TimeSpan.FromHours(1)),
            CancellationToken.None);

        CapturedPathAndQuery.Should().Be(
            "/v3/cryptocurrency/quotes/historical?id=1&convert=USD&time_start=1704067200&time_end=1706659200&count=31&interval=daily&skip_invalid=true");
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(12, 1)]
    [InlineData(24, 2)]
    [InlineData(36, 2)]
    [InlineData(47, 2)]
    [InlineData(720, 31)]
    public async Task Should_AskForOnePointPerWholeDayPlusOne_When_RequestingHistoricalQuotes(int rangeHours, int expectedCount)
    {
        var client = CreateClient("null");
        var timeStart = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        await client.GetHistoricalQuotesAsync(1, "USD", timeStart, timeStart.AddHours(rangeHours), CancellationToken.None);

        CapturedPathAndQuery.Should().Contain($"&count={expectedCount}&");
    }

    [Fact]
    public async Task Should_PercentEncodeTheCurrency_When_RequestingHistoricalQuotes()
    {
        var client = CreateClient("null");

        await client.GetHistoricalQuotesAsync(1, "U&SD", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, CancellationToken.None);

        CapturedPathAndQuery.Should().Be(
            "/v3/cryptocurrency/quotes/historical?id=1&convert=U%26SD&time_start=0&time_end=0&count=1&interval=daily&skip_invalid=true");
    }

    [Fact]
    public async Task Should_ReturnTheCoinsQuotesKeyedById_When_HistoricalQuotesResponseIsSuccessful()
    {
        var client = CreateClient(
            "{" + Status + """
            ,"data":{"1":{"id":1,"name":"Bitcoin","symbol":"BTC","is_active":1,"is_fiat":0,"quotes":[
              {"timestamp":"2024-01-01T23:59:59.999Z","quote":{"USD":{"price":42265.19,"volume_24h":16397000000.1,"market_cap":828000000000,"percent_change_24h":0.5,"timestamp":"2024-01-01T23:59:59.999Z"}}},
              {"timestamp":"2024-01-02T23:59:59.999Z","quote":{"USD":{"price":44167.33,"volume_24h":null,"market_cap":null,"timestamp":"2024-01-02T23:59:59.999Z"}}}
            ]}}}
            """);

        var response = await client.GetHistoricalQuotesAsync(1, "USD", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, CancellationToken.None);

        var firstDay = new DateTimeOffset(2024, 1, 1, 23, 59, 59, 999, TimeSpan.Zero);
        var secondDay = new DateTimeOffset(2024, 1, 2, 23, 59, 59, 999, TimeSpan.Zero);
        response.IsSuccess.Should().BeTrue();
        response.Value.Should().BeEquivalentTo(new Dictionary<string, CoinMarketCapHistoricalCoin>
        {
            ["1"] = new()
            {
                Quotes =
                [
                    new CoinMarketCapHistoricalQuote
                    {
                        Timestamp = firstDay,
                        Quote = new Dictionary<string, CoinMarketCapHistoricalCurrencyQuote>
                        {
                            ["USD"] = new() { Price = 42265.19m, Volume24h = 16397000000.1m, MarketCap = 828000000000m },
                        },
                    },
                    new CoinMarketCapHistoricalQuote
                    {
                        Timestamp = secondDay,
                        Quote = new Dictionary<string, CoinMarketCapHistoricalCurrencyQuote>
                        {
                            ["USD"] = new() { Price = 44167.33m },
                        },
                    },
                ],
            },
        });
    }

    // The members a later step reads are required: a success body that lacks one fails the call.

    [Theory]
    [InlineData("id", """{"data":[{"slug":"bitcoin","quote":[]}]}""")]
    [InlineData("slug", """{"data":[{"id":1,"quote":[]}]}""")]
    [InlineData("quote", """{"data":[{"id":1,"slug":"bitcoin"}]}""")]
    [InlineData("quote[].symbol", """{"data":[{"id":1,"slug":"bitcoin","quote":[{"price":1}]}]}""")]
    public async Task Should_ThrowJsonException_When_LatestQuotesBodyLacksARequiredField(string missing, string body)
    {
        _ = missing;
        var client = CreateClient(body);

        var act = () => client.GetLatestQuotesAsync(["bitcoin"], ["USD"], CancellationToken.None);

        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }

    [Theory]
    [InlineData("id", """{"data":{"bitcoin":{"name":"Bitcoin","symbol":"BTC","slug":"bitcoin"}}}""")]
    [InlineData("name", """{"data":{"bitcoin":{"id":1,"symbol":"BTC","slug":"bitcoin"}}}""")]
    [InlineData("symbol", """{"data":{"bitcoin":{"id":1,"name":"Bitcoin","slug":"bitcoin"}}}""")]
    [InlineData("slug", """{"data":{"bitcoin":{"id":1,"name":"Bitcoin","symbol":"BTC"}}}""")]
    [InlineData("contract_address[].contract_address", """{"data":{"usd-coin":{"id":3408,"name":"USDC","symbol":"USDC","slug":"usd-coin","contract_address":[{"platform":{"coin":{"slug":"ethereum"}}}]}}}""")]
    [InlineData("contract_address[].platform", """{"data":{"usd-coin":{"id":3408,"name":"USDC","symbol":"USDC","slug":"usd-coin","contract_address":[{"contract_address":"0xa0b8"}]}}}""")]
    [InlineData("contract_address[].platform.coin", """{"data":{"usd-coin":{"id":3408,"name":"USDC","symbol":"USDC","slug":"usd-coin","contract_address":[{"contract_address":"0xa0b8","platform":{"name":"Ethereum"}}]}}}""")]
    [InlineData("contract_address[].platform.coin.slug", """{"data":{"usd-coin":{"id":3408,"name":"USDC","symbol":"USDC","slug":"usd-coin","contract_address":[{"contract_address":"0xa0b8","platform":{"coin":{"id":"1027"}}}]}}}""")]
    public async Task Should_ThrowJsonException_When_InfoBodyLacksARequiredField(string missing, string body)
    {
        _ = missing;
        var client = CreateClient(body);

        var act = () => client.GetInfoBySlugAsync(["bitcoin"], CancellationToken.None);

        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }

    [Theory]
    [InlineData("quotes", """{"data":{"1":{"id":1,"name":"Bitcoin","symbol":"BTC"}}}""")]
    [InlineData("quotes[].timestamp", """{"data":{"1":{"quotes":[{"quote":{"USD":{"price":1}}}]}}}""")]
    [InlineData("quotes[].quote", """{"data":{"1":{"quotes":[{"timestamp":"2024-01-01T23:59:59.999Z"}]}}}""")]
    public async Task Should_ThrowJsonException_When_HistoricalQuotesBodyLacksARequiredField(string missing, string body)
    {
        _ = missing;
        var client = CreateClient(body);

        var act = () => client.GetHistoricalQuotesAsync(1, "USD", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, CancellationToken.None);

        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }
}
