using System.Net;
using CryptoStuff.CoinMarketCap.UnitTests.TestSupport;
using CryptoStuff.Core;
using Microsoft.Extensions.Time.Testing;
using static CryptoStuff.CoinMarketCap.UnitTests.TestSupport.CoinMarketCapScript;

namespace CryptoStuff.CoinMarketCap.UnitTests;

public class CoinMarketCapCryptocurrencyServiceCoinTests
{
    private static CoinMarketCapCryptocurrencyService CreateService(ICoinMarketCapClient client) =>
        new(client, new FakeTimeProvider(), new CoinMarketCapServiceOptions());

    private static CoinMarketCapCryptocurrencyService CreateService(ICoinMarketCapClient client, params string[] detailCurrencies) =>
        new(client, new FakeTimeProvider(), new CoinMarketCapServiceOptions { DetailCurrencies = detailCurrencies });

    private static CoinQuery Query(string coinId) => new() { CoinId = coinId };

    private static CoinView ValueOf(ServiceResult<CoinView> result)
    {
        result.IsSuccess.Should().BeTrue();
        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        return result.Value!;
    }

    /// <summary>A script answering <c>info</c> with <paramref name="info"/> and <c>quotes/latest</c> by currency from <paramref name="latest"/>.</summary>
    private static Func<string, object> Script(object info, IReadOnlyDictionary<string, object> latest) =>
        requestUri => IsInfo(requestUri) ? info : LatestByCurrency(latest)(requestUri);

    private static CoinMarketCapResponse<IReadOnlyDictionary<string, CoinMarketCapCoinInfo>> BnbInfo() =>
        Info(InfoCoin("bnb", 1839, name: "BNB", symbol: "BNB", description: "BNB is the coin of BNB Chain.", logo: "https://example.test/1839.png"));

    [Fact]
    public async Task Should_ReturnTheCoinUnderTheRequestedIdWithASnapshotPerDetailCurrency_When_EveryCallSucceeds()
    {
        var client = new FakeCoinMarketCapClient(Script(BnbInfo(), new Dictionary<string, object>
        {
            ["USD"] = Latest(Coin("bnb", 1839, Quote("USD", 600m, 90000000000m, 1500000000m))),
            ["EUR"] = Latest(Coin("bnb", 1839, Quote("EUR", 550m, 82000000000m, 1400000000m))),
        }));

        var result = await CreateService(client, "usd", "eur").GetCoinAsync(Query("binancecoin"), CancellationToken.None);

        ValueOf(result).Should().BeEquivalentTo(new CoinView
        {
            Id = "binancecoin",
            Symbol = "bnb",
            Name = "BNB",
            Description = "BNB is the coin of BNB Chain.",
            ImageUrl = "https://example.test/1839.png",
            MarketData =
            [
                new CoinCurrencySnapshot { Currency = "eur", Price = 550m, MarketCap = 82000000000m, Volume = 1400000000m },
                new CoinCurrencySnapshot { Currency = "usd", Price = 600m, MarketCap = 90000000000m, Volume = 1500000000m },
            ],
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Should_QuoteTheSlugInEachDetailCurrencyThenAskForItsInfo_When_ReadingACoin()
    {
        var client = new FakeCoinMarketCapClient(Script(BnbInfo(), new Dictionary<string, object>
        {
            ["USD"] = Latest(Coin("bnb", 1839, Quote("USD", 600m, 90000000000m, 1500000000m))),
        }));

        await CreateService(client).GetCoinAsync(Query("binancecoin"), CancellationToken.None);

        client.RequestUris.Should().Equal(
            "v3/cryptocurrency/quotes/latest?slug=bnb&convert=USD&skip_invalid=true",
            "v2/cryptocurrency/info?slug=bnb");
    }

    [Fact]
    public async Task Should_ReturnNotFoundAndMakeNoCall_When_TheCoinIdIsAnotherCoinsOverrideSlug()
    {
        var client = new FakeCoinMarketCapClient(_ => throw new InvalidOperationException("No call expected."));

        var result = await CreateService(client).GetCoinAsync(Query("bnb"), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        client.RequestUris.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnNotFoundWithNoInfoCall_When_TheCoinIsMissingFromTheQuotes()
    {
        var client = new FakeCoinMarketCapClient(Script(BnbInfo(), new Dictionary<string, object> { ["USD"] = Latest() }));

        var result = await CreateService(client).GetCoinAsync(Query("binancecoin"), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        client.RequestUris.Should().NotContain(uri => IsInfo(uri));
    }

    [Fact]
    public async Task Should_PassTheFailureOn_When_AQuoteCallFails()
    {
        var client = new FakeCoinMarketCapClient(Script(BnbInfo(), new Dictionary<string, object> { ["USD"] = LatestFailure(HttpStatusCode.TooManyRequests) }));

        var result = await CreateService(client).GetCoinAsync(Query("binancecoin"), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.RateLimited);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, ServiceErrorCode.RateLimited)]
    [InlineData(HttpStatusCode.BadRequest, ServiceErrorCode.UpstreamUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError, ServiceErrorCode.UpstreamUnavailable)]
    public async Task Should_FailWithTheMappedCode_When_TheInfoCallFails(HttpStatusCode statusCode, ServiceErrorCode expected)
    {
        var client = new FakeCoinMarketCapClient(Script(InfoFailure(statusCode), new Dictionary<string, object>
        {
            ["USD"] = Latest(Coin("bnb", 1839, Quote("USD", 600m, 1m, 1m))),
        }));

        var result = await CreateService(client).GetCoinAsync(Query("binancecoin"), CancellationToken.None);

        result.ErrorCode.Should().Be(expected);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_TheInfoAnswerHoldsNoCoinWithTheSlug()
    {
        var client = new FakeCoinMarketCapClient(Script(Info(InfoCoin("bnb-chain", 1)), new Dictionary<string, object>
        {
            ["USD"] = Latest(Coin("bnb", 1839, Quote("USD", 600m, 1m, 1m))),
        }));

        var result = await CreateService(client).GetCoinAsync(Query("binancecoin"), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_MatchTheInfoCoinBySlugWhateverItsKey_When_TheAnswerHoldsSeveral()
    {
        var client = new FakeCoinMarketCapClient(Script(
            Info(InfoCoin("bnb-chain", 1, name: "Other"), InfoCoin("bnb", 1839, name: "BNB")),
            new Dictionary<string, object> { ["USD"] = Latest(Coin("bnb", 1839, Quote("USD", 600m, 1m, 1m))) }));

        var result = await CreateService(client).GetCoinAsync(Query("binancecoin"), CancellationToken.None);

        ValueOf(result).Name.Should().Be("BNB");
    }

    [Fact]
    public async Task Should_LeaveOutASnapshot_When_TheQuoteLacksAnyOfPriceMarketCapOrVolume()
    {
        var client = new FakeCoinMarketCapClient(Script(BnbInfo(), new Dictionary<string, object>
        {
            ["USD"] = Latest(Coin("bnb", 1839, Quote("USD", 600m, 90000000000m, 1500000000m))),
            ["EUR"] = Latest(Coin("bnb", 1839, Quote("EUR", 550m, null, 1400000000m))),
            ["GBP"] = Latest(Coin("bnb", 1839, Quote("GBP", null, 70000000000m, 1200000000m))),
            ["JPY"] = Latest(Coin("bnb", 1839, Quote("JPY", 90000m, 1m, null))),
        }));

        var result = await CreateService(client, "usd", "eur", "gbp", "jpy").GetCoinAsync(Query("binancecoin"), CancellationToken.None);

        ValueOf(result).MarketData.Should().ContainSingle().Which.Currency.Should().Be("usd");
    }

    [Fact]
    public async Task Should_ReturnNoMarketData_When_NoDetailCurrencyHasAFullSnapshot()
    {
        var client = new FakeCoinMarketCapClient(Script(BnbInfo(), new Dictionary<string, object>
        {
            ["USD"] = Latest(Coin("bnb", 1839, Quote("USD", null))),
        }));

        var result = await CreateService(client).GetCoinAsync(Query("binancecoin"), CancellationToken.None);

        ValueOf(result).MarketData.Should().BeNull();
    }

    [Fact]
    public async Task Should_ReturnTheCoinWithNoMarketData_When_EveryDetailCurrencyIsRejected()
    {
        var client = new FakeCoinMarketCapClient(Script(BnbInfo(), new Dictionary<string, object>
        {
            ["XYZ"] = RejectedCurrency("XYZ"),
            ["USD"] = Latest(Coin("bnb", 1839, Quote("USD", 600m, 1m, 1m))),
        }));

        var result = await CreateService(client, "xyz").GetCoinAsync(Query("binancecoin"), CancellationToken.None);

        ValueOf(result).MarketData.Should().BeNull();
    }

    [Fact]
    public async Task Should_SettleWithOneUsdCallAndReturnNoMarketData_When_NoDetailCurrencyIsConfigured()
    {
        var client = new FakeCoinMarketCapClient(Script(BnbInfo(), new Dictionary<string, object>
        {
            ["USD"] = Latest(Coin("bnb", 1839, Quote("USD", 600m, 1m, 1m))),
        }));

        var result = await CreateService(client, detailCurrencies: []).GetCoinAsync(Query("binancecoin"), CancellationToken.None);

        ValueOf(result).MarketData.Should().BeNull();
        client.RequestUris.Should().Equal(
            "v3/cryptocurrency/quotes/latest?slug=bnb&convert=USD&skip_invalid=true",
            "v2/cryptocurrency/info?slug=bnb");
    }

    [Fact]
    public async Task Should_PassTheCallersCancellationToken_When_Calling()
    {
        using var cancellationSource = new CancellationTokenSource();
        var client = new FakeCoinMarketCapClient(Script(BnbInfo(), new Dictionary<string, object>
        {
            ["USD"] = Latest(Coin("bnb", 1839, Quote("USD", 600m, 1m, 1m))),
        }));

        await CreateService(client).GetCoinAsync(Query("binancecoin"), cancellationSource.Token);

        client.CancellationTokens.Should().Equal(cancellationSource.Token, cancellationSource.Token);
    }

    // Upstream JSON: `required` rejects only a missing field, so an explicit
    // null still reaches the service at each of these points.

    public static TheoryData<string> InfoNullPoints => new() { "info data", "coin", "coin slug" };

    [Theory]
    [MemberData(nameof(InfoNullPoints))]
    public async Task Should_ReturnNotFound_When_TheInfoAnswerIsNullAt(string point)
    {
        var client = new FakeCoinMarketCapClient(Script(InfoNullAt(point), new Dictionary<string, object>
        {
            ["USD"] = Latest(Coin("bnb", 1839, Quote("USD", 600m, 1m, 1m))),
        }));

        var result = await CreateService(client).GetCoinAsync(Query("binancecoin"), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    // `!` is deliberate throughout: each case hands over the null upstream JSON can carry.
    private static CoinMarketCapResponse<IReadOnlyDictionary<string, CoinMarketCapCoinInfo>> InfoNullAt(string point) => point switch
    {
        "info data" => CoinMarketCapResponse<IReadOnlyDictionary<string, CoinMarketCapCoinInfo>>.Success(null, HttpStatusCode.OK),
        "coin" => Info([null!]),
        "coin slug" => Info(InfoCoin(null!, 1839)),
        _ => throw new ArgumentOutOfRangeException(nameof(point), point, "Not a null point the test knows."),
    };

    [Fact]
    public async Task Should_ReturnEmptyNameAndSymbol_When_InfoCarriesThemAsNull()
    {
        // `!` is deliberate: the test hands over the nulls upstream JSON can carry.
        var client = new FakeCoinMarketCapClient(Script(Info(InfoCoin("bnb", 1839, name: null!, symbol: null!)), new Dictionary<string, object>
        {
            ["USD"] = Latest(Coin("bnb", 1839, Quote("USD", 600m, 1m, 1m))),
        }));

        var result = await CreateService(client).GetCoinAsync(Query("binancecoin"), CancellationToken.None);

        var coin = ValueOf(result);
        coin.Name.Should().BeEmpty();
        coin.Symbol.Should().BeEmpty();
    }
}
