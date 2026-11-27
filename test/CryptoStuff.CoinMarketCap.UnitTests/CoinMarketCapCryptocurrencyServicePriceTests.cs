using System.Net;
using CryptoStuff.CoinMarketCap.UnitTests.TestSupport;
using CryptoStuff.Core;
using Microsoft.Extensions.Time.Testing;
using static CryptoStuff.CoinMarketCap.UnitTests.TestSupport.CoinMarketCapScript;

namespace CryptoStuff.CoinMarketCap.UnitTests;

public class CoinMarketCapCryptocurrencyServicePriceTests
{
    private static CoinMarketCapCryptocurrencyService CreateService(ICoinMarketCapClient client) =>
        new(client, new FakeTimeProvider(), new CoinMarketCapServiceOptions());

    private static CoinPriceQuery Query(string[] coinIds, string[] currencies) => new() { CoinIds = coinIds, VsCurrencies = currencies };

    private static CoinPriceView ValueOf(ServiceResult<CoinPriceView> result)
    {
        result.IsSuccess.Should().BeTrue();
        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        return result.Value!;
    }

    [Fact]
    public async Task Should_PriceEachCoinInEachCurrencyInQueryOrder_When_EveryCallSucceeds()
    {
        var client = new FakeCoinMarketCapClient(LatestByCurrency(new Dictionary<string, object>
        {
            ["USD"] = Latest(Coin("bnb", 1839, Quote("USD", 600m)), Coin("bitcoin", 1, Quote("USD", 50000m))),
            ["EUR"] = Latest(Coin("bitcoin", 1, Quote("EUR", 46000m)), Coin("bnb", 1839, Quote("EUR", 550m))),
        }));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin", "binancecoin"], ["usd", "eur"]), CancellationToken.None);

        ValueOf(result).Coins.Should().BeEquivalentTo(
            [
                new CoinPrice
                {
                    CoinId = "bitcoin",
                    Amounts = [new CurrencyAmount { Currency = "usd", Amount = 50000m }, new CurrencyAmount { Currency = "eur", Amount = 46000m }],
                },
                new CoinPrice
                {
                    CoinId = "binancecoin",
                    Amounts = [new CurrencyAmount { Currency = "usd", Amount = 600m }, new CurrencyAmount { Currency = "eur", Amount = 550m }],
                },
            ],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Should_AskForEveryCoinOncePerCurrencyInUpperCase_When_PricingCoins()
    {
        var client = new FakeCoinMarketCapClient(LatestByCurrency(new Dictionary<string, object>
        {
            ["USD"] = Latest(Coin("bitcoin", 1, Quote("USD", 1m)), Coin("bnb", 1839, Quote("USD", 1m))),
            ["EUR"] = Latest(Coin("bitcoin", 1, Quote("EUR", 1m)), Coin("bnb", 1839, Quote("EUR", 1m))),
        }));

        await CreateService(client).GetPricesAsync(Query(["bitcoin", "binancecoin", "bitcoin"], ["usd", "eur", "USD"]), CancellationToken.None);

        client.RequestUris.Should().Equal(
            "v3/cryptocurrency/quotes/latest?slug=bitcoin,bnb&convert=USD&skip_invalid=true",
            "v3/cryptocurrency/quotes/latest?slug=bitcoin,bnb&convert=EUR&skip_invalid=true");
    }

    [Fact]
    public async Task Should_GiveEachCurrencyOneAmount_When_QueryRepeatsItInAnotherCase()
    {
        var client = new FakeCoinMarketCapClient(_ => Latest(Coin("bitcoin", 1, Quote("USD", 50000m))));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin"], ["usd", "USD"]), CancellationToken.None);

        ValueOf(result).Coins.Should().ContainSingle()
            .Which.Amounts.Should().Equal(new CurrencyAmount { Currency = "usd", Amount = 50000m });
    }

    [Fact]
    public async Task Should_ListARepeatedCoinIdEachTime_When_QueryRepeatsIt()
    {
        var client = new FakeCoinMarketCapClient(_ => Latest(Coin("bitcoin", 1, Quote("USD", 50000m))));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin", "bitcoin"], ["usd"]), CancellationToken.None);

        ValueOf(result).Coins.Select(coin => coin.CoinId).Should().Equal("bitcoin", "bitcoin");
    }

    [Fact]
    public async Task Should_ReturnNoCoinsAndMakeNoCall_When_QueryNamesNoCoins()
    {
        var client = new FakeCoinMarketCapClient(_ => throw new InvalidOperationException("No call expected."));

        var result = await CreateService(client).GetPricesAsync(Query([], ["usd"]), CancellationToken.None);

        ValueOf(result).Coins.Should().BeEmpty();
        client.RequestUris.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnNotFoundAndMakeNoCall_When_ACoinIdIsAnotherCoinsOverrideSlug()
    {
        var client = new FakeCoinMarketCapClient(_ => throw new InvalidOperationException("No call expected."));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin", "xrp"], ["usd"]), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        client.RequestUris.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnNotFoundAndStopCalling_When_ACoinIsMissingFromAResponse()
    {
        var client = new FakeCoinMarketCapClient(LatestByCurrency(new Dictionary<string, object>
        {
            ["USD"] = Latest(Coin("bitcoin", 1, Quote("USD", 50000m))),
            ["EUR"] = Latest(Coin("bitcoin", 1, Quote("EUR", 46000m)), Coin("ethereum", 1027, Quote("EUR", 2000m))),
        }));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin", "ethereum"], ["usd", "eur"]), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        client.RequestUris.Should().ContainSingle();
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_ACoinIsMissingFromALaterResponse()
    {
        var client = new FakeCoinMarketCapClient(LatestByCurrency(new Dictionary<string, object>
        {
            ["USD"] = Latest(Coin("bitcoin", 1, Quote("USD", 50000m)), Coin("ethereum", 1027, Quote("USD", 2000m))),
            ["EUR"] = Latest(Coin("bitcoin", 1, Quote("EUR", 46000m))),
        }));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin", "ethereum"], ["usd", "eur"]), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_LeaveCoinsWithoutAnAmountInACurrency_When_CoinMarketCapRejectsThatCurrency()
    {
        var client = new FakeCoinMarketCapClient(LatestByCurrency(new Dictionary<string, object>
        {
            ["USD"] = Latest(Coin("bitcoin", 1, Quote("USD", 50000m))),
            ["XYZ"] = RejectedCurrency("XYZ"),
        }));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin"], ["xyz", "usd"]), CancellationToken.None);

        ValueOf(result).Coins.Should().ContainSingle()
            .Which.Amounts.Should().Equal(new CurrencyAmount { Currency = "usd", Amount = 50000m });
    }

    [Fact]
    public async Task Should_SettleWithOneUsdCallAndReturnNoAmounts_When_EveryCurrencyIsRejected()
    {
        var client = new FakeCoinMarketCapClient(LatestByCurrency(new Dictionary<string, object>
        {
            ["XYZ"] = RejectedCurrency("XYZ"),
            ["ABC"] = RejectedCurrency("ABC"),
            ["USD"] = Latest(Coin("bitcoin", 1, Quote("USD", 50000m))),
        }));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin"], ["xyz", "abc"]), CancellationToken.None);

        ValueOf(result).Coins.Should().ContainSingle().Which.Amounts.Should().BeEmpty();
        client.RequestUris.Select(uri => QueryValue(uri, "convert")).Should().Equal("XYZ", "ABC", "USD");
    }

    [Fact]
    public async Task Should_SettleWithOneUsdCallAndReturnNoAmounts_When_QueryNamesNoCurrency()
    {
        var client = new FakeCoinMarketCapClient(LatestByCurrency(new Dictionary<string, object>
        {
            ["USD"] = Latest(Coin("bitcoin", 1, Quote("USD", 50000m))),
        }));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin"], []), CancellationToken.None);

        ValueOf(result).Coins.Should().ContainSingle().Which.Amounts.Should().BeEmpty();
        client.RequestUris.Should().Equal("v3/cryptocurrency/quotes/latest?slug=bitcoin&convert=USD&skip_invalid=true");
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_QueryNamesNoCurrencyAndTheUsdCallMissesACoin()
    {
        var client = new FakeCoinMarketCapClient(LatestByCurrency(new Dictionary<string, object>
        {
            ["USD"] = Latest(Coin("bitcoin", 1, Quote("USD", 50000m))),
        }));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin", "ethereum"], []), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_EveryCurrencyIsRejectedAndTheUsdCallMissesACoin()
    {
        var client = new FakeCoinMarketCapClient(LatestByCurrency(new Dictionary<string, object>
        {
            ["XYZ"] = RejectedCurrency("XYZ"),
            ["USD"] = Latest(Coin("bitcoin", 1, Quote("USD", 50000m))),
        }));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin", "ethereum"], ["xyz"]), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_PassTheFailureOn_When_TheUsdSettlingCallFails()
    {
        var client = new FakeCoinMarketCapClient(LatestByCurrency(new Dictionary<string, object>
        {
            ["XYZ"] = RejectedCurrency("XYZ"),
            ["USD"] = LatestFailure(HttpStatusCode.TooManyRequests),
        }));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin"], ["xyz"]), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.RateLimited);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, null, ServiceErrorCode.RateLimited)]
    [InlineData(HttpStatusCode.Forbidden, null, ServiceErrorCode.UpstreamUnavailable)]
    [InlineData(HttpStatusCode.BadRequest, "Invalid value for \"aux\": \"x\"", ServiceErrorCode.UpstreamUnavailable)]
    public async Task Should_FailTheWholeResult_When_AnyOneCallFails(HttpStatusCode statusCode, string? errorMessage, ServiceErrorCode expected)
    {
        var client = new FakeCoinMarketCapClient(LatestByCurrency(new Dictionary<string, object>
        {
            ["USD"] = Latest(Coin("bitcoin", 1, Quote("USD", 50000m))),
            ["EUR"] = LatestFailure(statusCode, errorMessage),
        }));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin"], ["usd", "eur"]), CancellationToken.None);

        result.ErrorCode.Should().Be(expected);
    }

    [Fact]
    public async Task Should_ReturnNotFoundAndStopCalling_When_CoinMarketCapRejectsTheSlugs()
    {
        var client = new FakeCoinMarketCapClient(_ => LatestFailure(HttpStatusCode.BadRequest, "Invalid value for \"slug\": \"not-a-coin\""));

        var result = await CreateService(client).GetPricesAsync(Query(["not-a-coin"], ["usd", "eur"]), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        client.RequestUris.Should().ContainSingle();
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_TheUsdSettlingCallRejectsTheSlugs()
    {
        var client = new FakeCoinMarketCapClient(LatestByCurrency(new Dictionary<string, object>
        {
            ["XYZ"] = RejectedCurrency("XYZ"),
            ["USD"] = LatestFailure(HttpStatusCode.BadRequest, "Invalid value for \"slug\": \"not-a-coin\""),
        }));

        var result = await CreateService(client).GetPricesAsync(Query(["not-a-coin"], ["xyz"]), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnTheTimeout_When_ACallTimesOut()
    {
        var client = new FakeCoinMarketCapClient(_ => CoinMarketCapResponse<IReadOnlyList<CoinMarketCapLatestQuote>>.Timeout());

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin"], ["usd"]), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.RequestTimedOut);
    }

    [Fact]
    public async Task Should_LeaveACoinWithoutAnAmount_When_ItsQuoteHasNoPrice()
    {
        var client = new FakeCoinMarketCapClient(_ => Latest(Coin("bitcoin", 1, Quote("USD", null))));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin"], ["usd"]), CancellationToken.None);

        ValueOf(result).Coins.Should().ContainSingle().Which.Amounts.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_PassANegativePriceThroughUnchanged_When_CoinMarketCapQuotesOne()
    {
        // Milestone 8.3's guard, above every provider, drops it.
        var client = new FakeCoinMarketCapClient(_ => Latest(Coin("bitcoin", 1, Quote("USD", -1m))));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin"], ["usd"]), CancellationToken.None);

        ValueOf(result).Coins.Should().ContainSingle()
            .Which.Amounts.Should().Equal(new CurrencyAmount { Currency = "usd", Amount = -1m });
    }

    [Fact]
    public async Task Should_PassTheCallersCancellationToken_When_Calling()
    {
        using var cancellationSource = new CancellationTokenSource();
        var client = new FakeCoinMarketCapClient(_ => Latest(Coin("bitcoin", 1, Quote("USD", 1m))));

        await CreateService(client).GetPricesAsync(Query(["bitcoin"], ["usd"]), cancellationSource.Token);

        client.CancellationTokens.Should().Equal(cancellationSource.Token);
    }

    [Fact]
    public async Task Should_PassTheCallersCancellationTokenToTheSettlingCall_When_EveryCurrencyIsRejected()
    {
        using var cancellationSource = new CancellationTokenSource();
        var client = new FakeCoinMarketCapClient(LatestByCurrency(new Dictionary<string, object>
        {
            ["XYZ"] = RejectedCurrency("XYZ"),
            ["USD"] = Latest(Coin("bitcoin", 1, Quote("USD", 1m))),
        }));

        await CreateService(client).GetPricesAsync(Query(["bitcoin"], ["xyz"]), cancellationSource.Token);

        client.CancellationTokens.Should().Equal(cancellationSource.Token, cancellationSource.Token);
    }

    // Upstream JSON: `required` rejects only a missing field, so an explicit
    // null still reaches the service for each of these.

    [Fact]
    public async Task Should_ReturnNotFound_When_DataIsNull()
    {
        var client = new FakeCoinMarketCapClient(_ => CoinMarketCapResponse<IReadOnlyList<CoinMarketCapLatestQuote>>.Success(null, HttpStatusCode.OK));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin"], ["usd"]), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_SkipANullCoinEntry_When_DataHoldsOne()
    {
        // `!` is deliberate: the test hands over the null upstream JSON can carry.
        var client = new FakeCoinMarketCapClient(_ => Latest(null!, Coin("bitcoin", 1, Quote("USD", 50000m))));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin"], ["usd"]), CancellationToken.None);

        ValueOf(result).Coins.Should().ContainSingle().Which.Amounts.Should().ContainSingle();
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_TheCoinsSlugIsNull()
    {
        // `!` is deliberate: the test hands over the null upstream JSON can carry.
        var client = new FakeCoinMarketCapClient(_ => Latest(Coin(null!, 1, Quote("USD", 50000m))));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin"], ["usd"]), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_LeaveACoinWithoutAmounts_When_ItsQuoteListIsNull()
    {
        // `!` is deliberate: the test hands over the null upstream JSON can carry.
        var client = new FakeCoinMarketCapClient(_ => Latest(new CoinMarketCapLatestQuote { Id = 1, Slug = "bitcoin", Quote = null! }));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin"], ["usd"]), CancellationToken.None);

        ValueOf(result).Coins.Should().ContainSingle().Which.Amounts.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_SkipANullQuoteOrOneWithANullSymbol_When_TheQuoteListHoldsThem()
    {
        // `!` is deliberate: the test hands over the nulls upstream JSON can carry.
        var client = new FakeCoinMarketCapClient(_ => Latest(Coin("bitcoin", 1, null!, Quote(null!, 1m), Quote("USD", 50000m))));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin"], ["usd"]), CancellationToken.None);

        ValueOf(result).Coins.Should().ContainSingle()
            .Which.Amounts.Should().Equal(new CurrencyAmount { Currency = "usd", Amount = 50000m });
    }

    [Fact]
    public async Task Should_IgnoreAQuoteInAnotherCurrency_When_TheAnswerHoldsOne()
    {
        var client = new FakeCoinMarketCapClient(_ => Latest(Coin("bitcoin", 1, Quote("EUR", 46000m))));

        var result = await CreateService(client).GetPricesAsync(Query(["bitcoin"], ["usd"]), CancellationToken.None);

        ValueOf(result).Coins.Should().ContainSingle().Which.Amounts.Should().BeEmpty();
    }
}
