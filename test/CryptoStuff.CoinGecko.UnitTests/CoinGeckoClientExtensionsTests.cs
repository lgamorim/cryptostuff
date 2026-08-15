using System.Net;
using System.Text.Json;
using CryptoStuff.CoinGecko.UnitTests.TestSupport;

namespace CryptoStuff.CoinGecko.UnitTests;

public class CoinGeckoClientExtensionsTests
{
    [Fact]
    public async Task Should_BuildCommaJoinedPercentEncodedRequestUri_When_RequestingSimplePrice()
    {
        Uri? capturedUri = null;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "{}", onRequest: r => capturedUri = r.RequestUri);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        await client.GetSimplePriceAsync(["bitcoin", "eth,ereum"], ["usd", "eur"], CancellationToken.None);

        var uri = capturedUri ?? throw new InvalidOperationException("Request was not captured.");
        uri.PathAndQuery.Should().Be("/simple/price?ids=bitcoin,eth%2Cereum&vs_currencies=usd,eur");
    }

    [Fact]
    public async Task Should_BuildEmptyQueryParameters_When_NoIdentifiersOrCurrenciesProvided()
    {
        Uri? capturedUri = null;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "{}", onRequest: r => capturedUri = r.RequestUri);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        await client.GetSimplePriceAsync([], [], CancellationToken.None);

        var uri = capturedUri ?? throw new InvalidOperationException("Request was not captured.");
        uri.PathAndQuery.Should().Be("/simple/price?ids=&vs_currencies=");
    }

    [Fact]
    public async Task Should_ReturnDeserializedPriceMatrix_When_ResponseIsSuccessful()
    {
        var json = """{"bitcoin":{"usd":43189.52,"eur":39750.10},"ethereum":{"usd":2280.14,"eur":2100.55}}""";
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetSimplePriceAsync(["bitcoin", "ethereum"], ["usd", "eur"], CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        response.Value.Should().HaveCount(2);
        var value = response.Value ?? throw new InvalidOperationException("Expected a value.");
        value["bitcoin"]["usd"].Should().Be(43189.52m);
    }

    [Fact]
    public async Task Should_ReturnEmptyPriceMatrix_When_ResponseBodyIsEmptyJsonObject()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "{}");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetSimplePriceAsync(["unknown-coin"], ["usd"], CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        response.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ThrowJsonException_When_ResponseBodyIsMalformedJson()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "{not valid json");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var act = () => client.GetSimplePriceAsync(["bitcoin"], ["usd"], CancellationToken.None);

        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task Should_CaptureStatusCode_When_ResponseIsNotSuccessful()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.TooManyRequests, "");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetSimplePriceAsync(["bitcoin"], ["usd"], CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Should_ReturnTimeoutResponse_When_HttpClientTimeoutElapses()
    {
        var handler = FakeHttpMessageHandler.SimulatingTimeout();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetSimplePriceAsync(["bitcoin"], ["usd"], CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeTrue();
        response.StatusCode.Should().BeNull();
    }

    [Fact]
    public async Task Should_BuildPercentEncodedPlatformSegmentAndCommaJoinedRequestUri_When_RequestingSimpleTokenPrice()
    {
        Uri? capturedUri = null;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "{}", onRequest: r => capturedUri = r.RequestUri);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        await client.GetSimpleTokenPriceAsync("polygon pos", ["0xAbC", "0xDeF,123"], ["usd"], CancellationToken.None);

        var uri = capturedUri ?? throw new InvalidOperationException("Request was not captured.");
        uri.PathAndQuery.Should().Be("/simple/token_price/polygon%20pos?contract_addresses=0xAbC,0xDeF%2C123&vs_currencies=usd");
    }

    [Fact]
    public async Task Should_ReturnDeserializedPriceMatrix_When_RequestingSimpleTokenPriceAndResponseIsSuccessful()
    {
        var json = """{"0xabc":{"usd":1.23}}""";
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetSimpleTokenPriceAsync("ethereum", ["0xabc"], ["usd"], CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        var value = response.Value ?? throw new InvalidOperationException("Expected a value.");
        value["0xabc"]["usd"].Should().Be(1.23m);
    }

    [Fact]
    public async Task Should_BuildPercentEncodedCoinIdAndQueryString_When_RequestingMarketChart()
    {
        Uri? capturedUri = null;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, EmptyMarketChartJson, onRequest: r => capturedUri = r.RequestUri);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        await client.GetMarketChartAsync("bit coin", "u s d", 30, CancellationToken.None);

        var uri = capturedUri ?? throw new InvalidOperationException("Request was not captured.");
        uri.PathAndQuery.Should().Be("/coins/bit%20coin/market_chart?vs_currency=u%20s%20d&days=30");
    }

    [Fact]
    public async Task Should_ReturnDeserializedMarketChart_When_ResponseIsSuccessful()
    {
        var json = """
            {
                "prices": [[1700000000000, 43189.52], [1700003600000, 43200.11]],
                "market_caps": [[1700000000000, 850000000000]],
                "total_volumes": [[1700000000000, 25000000000]]
            }
            """;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetMarketChartAsync("bitcoin", "usd", 30, CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        var value = response.Value ?? throw new InvalidOperationException("Expected a value.");
        value.Prices.Should().HaveCount(2);
        value.Prices[0].Should().Be(new CoinGeckoMarketChartPoint(1700000000000, 43189.52m));
        value.MarketCaps.Should().ContainSingle().Which.Should().Be(new CoinGeckoMarketChartPoint(1700000000000, 850000000000m));
        value.TotalVolumes.Should().ContainSingle().Which.Should().Be(new CoinGeckoMarketChartPoint(1700000000000, 25000000000m));
    }

    [Fact]
    public async Task Should_ReturnEmptySeries_When_ResponseArraysAreEmpty()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, EmptyMarketChartJson);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetMarketChartAsync("bitcoin", "usd", 30, CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        var value = response.Value ?? throw new InvalidOperationException("Expected a value.");
        value.Prices.Should().BeEmpty();
        value.MarketCaps.Should().BeEmpty();
        value.TotalVolumes.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ThrowJsonException_When_MarketChartResponseIsMissingASeries()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """{"prices":[],"total_volumes":[]}""");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var act = () => client.GetMarketChartAsync("bitcoin", "usd", 30, CancellationToken.None);

        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task Should_ThrowJsonException_When_MarketChartResponseBodyIsMalformedJson()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "{not valid json");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var act = () => client.GetMarketChartAsync("bitcoin", "usd", 30, CancellationToken.None);

        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task Should_CaptureStatusCode_When_MarketChartResponseIsNotSuccessful()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.NotFound, "");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetMarketChartAsync("unknown-coin", "usd", 30, CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnTimeoutResponse_When_MarketChartHttpClientTimeoutElapses()
    {
        var handler = FakeHttpMessageHandler.SimulatingTimeout();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetMarketChartAsync("bitcoin", "usd", 30, CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeTrue();
        response.StatusCode.Should().BeNull();
    }

    [Fact]
    public async Task Should_BuildPercentEncodedCoinIdAndExplicitQueryFlags_When_RequestingCoin()
    {
        Uri? capturedUri = null;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, MinimalCoinJson, onRequest: r => capturedUri = r.RequestUri);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        await client.GetCoinAsync("bit coin", CancellationToken.None);

        var uri = capturedUri ?? throw new InvalidOperationException("Request was not captured.");
        uri.PathAndQuery.Should().Be("/coins/bit%20coin?localization=false&tickers=false&market_data=true&community_data=false&developer_data=false&sparkline=false");
    }

    [Fact]
    public async Task Should_ReturnDeserializedCoin_When_ResponseIsSuccessfulWithMarketDataAndDescriptionAndImagePresent()
    {
        var json = """
            {
                "id": "bitcoin",
                "symbol": "btc",
                "name": "Bitcoin",
                "description": { "en": "Bitcoin is a decentralized cryptocurrency.", "de": "Bitcoin ist eine dezentrale Kryptowaehrung." },
                "image": { "thumb": "https://example.test/thumb.png", "small": "https://example.test/small.png", "large": "https://example.test/large.png" },
                "market_data": {
                    "current_price": { "usd": 43189.52, "eur": 39750.10 },
                    "market_cap": { "usd": 850000000000, "eur": 780000000000 },
                    "total_volume": { "usd": 25000000000, "eur": 23000000000 }
                }
            }
            """;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetCoinAsync("bitcoin", CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        var value = response.Value ?? throw new InvalidOperationException("Expected a value.");
        value.Id.Should().Be("bitcoin");
        value.Symbol.Should().Be("btc");
        value.Name.Should().Be("Bitcoin");
        var description = value.Description ?? throw new InvalidOperationException("Expected a description.");
        description["en"].Should().Be("Bitcoin is a decentralized cryptocurrency.");
        value.Image.Large.Should().Be("https://example.test/large.png");
        var marketData = value.MarketData ?? throw new InvalidOperationException("Expected market data.");
        marketData.CurrentPrice["usd"].Should().Be(43189.52m);
        marketData.MarketCap["usd"].Should().Be(850000000000m);
        marketData.TotalVolume["usd"].Should().Be(25000000000m);
    }

    [Fact]
    public async Task Should_ReturnNullImageFields_When_ImageSizesAreExplicitlyNull()
    {
        var json = """
            {
                "id": "bitcoin",
                "symbol": "btc",
                "name": "Bitcoin",
                "image": { "thumb": null, "small": null, "large": null }
            }
            """;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetCoinAsync("bitcoin", CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        var value = response.Value ?? throw new InvalidOperationException("Expected a value.");
        value.Image.Thumb.Should().BeNull();
        value.Image.Small.Should().BeNull();
        value.Image.Large.Should().BeNull();
    }

    [Fact]
    public async Task Should_ReturnNullMarketData_When_MarketDataFieldIsAbsentFromResponse()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, MinimalCoinJson);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetCoinAsync("bitcoin", CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        var value = response.Value ?? throw new InvalidOperationException("Expected a value.");
        value.MarketData.Should().BeNull();
    }

    [Fact]
    public async Task Should_ReturnEmptyDescriptionDictionary_When_DescriptionObjectIsEmpty()
    {
        var json = """
            {
                "id": "bitcoin",
                "symbol": "btc",
                "name": "Bitcoin",
                "description": {},
                "image": { "thumb": null, "small": null, "large": null }
            }
            """;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetCoinAsync("bitcoin", CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        var value = response.Value ?? throw new InvalidOperationException("Expected a value.");
        value.Description.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_CaptureStatusCode_When_CoinResponseIsNotSuccessful()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.NotFound, "");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetCoinAsync("unknown-coin", CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnTimeoutResponse_When_CoinHttpClientTimeoutElapses()
    {
        var handler = FakeHttpMessageHandler.SimulatingTimeout();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetCoinAsync("bitcoin", CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeTrue();
        response.StatusCode.Should().BeNull();
    }

    [Fact]
    public async Task Should_ThrowJsonException_When_CoinResponseBodyIsMalformedJson()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "{not valid json");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var act = () => client.GetCoinAsync("bitcoin", CancellationToken.None);

        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task Should_ThrowJsonException_When_CoinResponseIsMissingRequiredImageField()
    {
        var json = """{"id":"bitcoin","symbol":"btc","name":"Bitcoin"}""";
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var act = () => client.GetCoinAsync("bitcoin", CancellationToken.None);

        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task Should_ReturnNullImage_When_ImageFieldIsExplicitlyNullDespiteBeingRequired()
    {
        var json = """{"id":"bitcoin","symbol":"btc","name":"Bitcoin","image":null}""";
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetCoinAsync("bitcoin", CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        var value = response.Value ?? throw new InvalidOperationException("Expected a value.");
        value.Image.Should().BeNull();
    }

    [Fact]
    public async Task Should_ReturnNullDescription_When_DescriptionFieldIsAbsentFromResponse()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, MinimalCoinJson);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetCoinAsync("bitcoin", CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        var value = response.Value ?? throw new InvalidOperationException("Expected a value.");
        value.Description.Should().BeNull();
    }

    [Fact]
    public async Task Should_ThrowJsonException_When_CoinMarketDataIsMissingARequiredCurrencyField()
    {
        var json = """
            {
                "id": "bitcoin",
                "symbol": "btc",
                "name": "Bitcoin",
                "image": { "thumb": null, "small": null, "large": null },
                "market_data": {
                    "current_price": { "usd": 43189.52 },
                    "market_cap": { "usd": 850000000000 }
                }
            }
            """;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var act = () => client.GetCoinAsync("bitcoin", CancellationToken.None);

        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task Should_BuildPercentEncodedCoinIdAndRawDateQueryParameter_When_RequestingCoinHistory()
    {
        Uri? capturedUri = null;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, MinimalCoinHistoryJson, onRequest: r => capturedUri = r.RequestUri);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        await client.GetCoinHistoryAsync("bit coin", "30-12-2023", CancellationToken.None);

        var uri = capturedUri ?? throw new InvalidOperationException("Request was not captured.");
        uri.PathAndQuery.Should().Be("/coins/bit%20coin/history?date=30-12-2023&localization=false");
    }

    [Fact]
    public async Task Should_ReturnDeserializedCoinHistory_When_ResponseIsSuccessfulWithDeveloperDataAndCodeChurnPresent()
    {
        var json = """
            {
                "id": "bitcoin",
                "symbol": "btc",
                "name": "Bitcoin",
                "developer_data": {
                    "forks": 35000,
                    "stars": 70000,
                    "subscribers": 3800,
                    "total_issues": 7500,
                    "closed_issues": 7200,
                    "pull_requests_merged": 10500,
                    "pull_request_contributors": 850,
                    "commit_count_4_weeks": 45,
                    "code_additions_deletions_4_weeks": { "additions": 1200, "deletions": 400 }
                }
            }
            """;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetCoinHistoryAsync("bitcoin", "30-12-2023", CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        var value = response.Value ?? throw new InvalidOperationException("Expected a value.");
        value.Id.Should().Be("bitcoin");
        var developerData = value.DeveloperData ?? throw new InvalidOperationException("Expected developer data.");
        developerData.Forks.Should().Be(35000);
        developerData.Stars.Should().Be(70000);
        developerData.CommitCount4Weeks.Should().Be(45);
        var codeChurn = developerData.CodeChurnLast4Weeks ?? throw new InvalidOperationException("Expected code churn.");
        codeChurn.Additions.Should().Be(1200);
        codeChurn.Deletions.Should().Be(400);
    }

    [Fact]
    public async Task Should_ReturnNullDeveloperData_When_DeveloperDataFieldIsAbsentFromResponse()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, MinimalCoinHistoryJson);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetCoinHistoryAsync("bitcoin", "30-12-2023", CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        var value = response.Value ?? throw new InvalidOperationException("Expected a value.");
        value.DeveloperData.Should().BeNull();
    }

    [Fact]
    public async Task Should_ReturnNullCodeChurn_When_CodeAdditionsDeletions4WeeksFieldIsAbsentFromDeveloperData()
    {
        var json = """
            {
                "id": "bitcoin",
                "symbol": "btc",
                "name": "Bitcoin",
                "developer_data": { "forks": 35000, "stars": 70000 }
            }
            """;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetCoinHistoryAsync("bitcoin", "30-12-2023", CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        var value = response.Value ?? throw new InvalidOperationException("Expected a value.");
        var developerData = value.DeveloperData ?? throw new InvalidOperationException("Expected developer data.");
        developerData.CodeChurnLast4Weeks.Should().BeNull();
    }

    [Fact]
    public async Task Should_ReturnNullAdditionsAndDeletions_When_CodeChurnFieldsAreExplicitlyNull()
    {
        var json = """
            {
                "id": "bitcoin",
                "symbol": "btc",
                "name": "Bitcoin",
                "developer_data": {
                    "code_additions_deletions_4_weeks": { "additions": null, "deletions": null }
                }
            }
            """;
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetCoinHistoryAsync("bitcoin", "30-12-2023", CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        var value = response.Value ?? throw new InvalidOperationException("Expected a value.");
        var developerData = value.DeveloperData ?? throw new InvalidOperationException("Expected developer data.");
        var codeChurn = developerData.CodeChurnLast4Weeks ?? throw new InvalidOperationException("Expected code churn.");
        codeChurn.Additions.Should().BeNull();
        codeChurn.Deletions.Should().BeNull();
    }

    [Fact]
    public async Task Should_CaptureStatusCode_When_CoinHistoryResponseIsNotSuccessful()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.NotFound, "");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetCoinHistoryAsync("unknown-coin", "30-12-2023", CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnTimeoutResponse_When_CoinHistoryHttpClientTimeoutElapses()
    {
        var handler = FakeHttpMessageHandler.SimulatingTimeout();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var response = await client.GetCoinHistoryAsync("bitcoin", "30-12-2023", CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeTrue();
        response.StatusCode.Should().BeNull();
    }

    [Fact]
    public async Task Should_ThrowJsonException_When_CoinHistoryResponseBodyIsMalformedJson()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, "{not valid json");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        ICoinGeckoClient client = new CoinGeckoClient(httpClient);

        var act = () => client.GetCoinHistoryAsync("bitcoin", "30-12-2023", CancellationToken.None);

        await act.Should().ThrowAsync<JsonException>();
    }

    private const string EmptyMarketChartJson = """{"prices":[],"market_caps":[],"total_volumes":[]}""";

    private const string MinimalCoinJson = """{"id":"bitcoin","symbol":"btc","name":"Bitcoin","image":{"thumb":null,"small":null,"large":null}}""";

    private const string MinimalCoinHistoryJson = """{"id":"bitcoin","symbol":"btc","name":"Bitcoin"}""";
}
