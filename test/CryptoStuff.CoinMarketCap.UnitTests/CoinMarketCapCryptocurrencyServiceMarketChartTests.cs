using System.Net;
using CryptoStuff.CoinMarketCap.UnitTests.TestSupport;
using CryptoStuff.Core;
using Microsoft.Extensions.Time.Testing;
using static CryptoStuff.CoinMarketCap.UnitTests.TestSupport.CoinMarketCapScript;

namespace CryptoStuff.CoinMarketCap.UnitTests;

public class CoinMarketCapCryptocurrencyServiceMarketChartTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private static CoinMarketCapCryptocurrencyService CreateService(ICoinMarketCapClient client) =>
        new(client, new FakeTimeProvider(Now), new CoinMarketCapServiceOptions());

    private static CoinMarketChartQuery Query(string coinId, string currency, int days) => new() { CoinId = coinId, VsCurrency = currency, Days = days };

    private static CoinMarketChartView ValueOf(ServiceResult<CoinMarketChartView> result)
    {
        result.IsSuccess.Should().BeTrue();
        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        return result.Value!;
    }

    /// <summary>A script answering <c>quotes/latest</c> with <paramref name="latest"/> and <c>quotes/historical</c> with <paramref name="historical"/>.</summary>
    private static Func<string, object> Script(object latest, object? historical = null) =>
        requestUri => IsHistorical(requestUri)
            ? historical ?? throw new InvalidOperationException("No quotes/historical call expected.")
            : latest;

    [Fact]
    public async Task Should_ReturnTheDailySeriesByUtcDate_When_HistoryIsAnswered()
    {
        var client = new FakeCoinMarketCapClient(Script(
            Latest(Coin("bitcoin", 1, Quote("USD", 1m))),
            Historical(
                1,
                Point("2026-11-25T23:59:59.999Z", "EUR", 40000m, 800000m, 30000m),
                Point("2026-11-26T23:59:59.999Z", "EUR", 41000m, 810000m, 31000m))));

        var result = await CreateService(client).GetMarketChartAsync(Query("bitcoin", "eur", 2), CancellationToken.None);

        ValueOf(result).Should().BeEquivalentTo(
            new CoinMarketChartView
            {
                Prices = [new CoinMarketChartPoint { Date = "2026-11-25", Value = 40000m }, new CoinMarketChartPoint { Date = "2026-11-26", Value = 41000m }],
                MarketCaps = [new CoinMarketChartPoint { Date = "2026-11-25", Value = 800000m }, new CoinMarketChartPoint { Date = "2026-11-26", Value = 810000m }],
                TotalVolumes = [new CoinMarketChartPoint { Date = "2026-11-25", Value = 30000m }, new CoinMarketChartPoint { Date = "2026-11-26", Value = 31000m }],
            },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Should_LookUpTheNumericIdInUsdThenAskForTheRequestedDays_When_ChartingACoin()
    {
        var client = new FakeCoinMarketCapClient(Script(Latest(Coin("bnb", 1839, Quote("USD", 1m))), Historical(1839)));

        await CreateService(client).GetMarketChartAsync(Query("binancecoin", "eur", 30), CancellationToken.None);

        var start = Now.AddDays(-30).ToUnixTimeSeconds();
        var end = Now.ToUnixTimeSeconds();
        client.RequestUris.Should().Equal(
            "v3/cryptocurrency/quotes/latest?slug=bnb&convert=USD&skip_invalid=true",
            $"v3/cryptocurrency/quotes/historical?id=1839&convert=EUR&time_start={start}&time_end={end}&count=31&interval=daily&skip_invalid=true");
    }

    [Fact]
    public async Task Should_AskForAFullYear_When_DaysAreExactlyBasicsLimit()
    {
        var client = new FakeCoinMarketCapClient(Script(Latest(Coin("bitcoin", 1, Quote("USD", 1m))), Historical(1)));

        var result = await CreateService(client).GetMarketChartAsync(Query("bitcoin", "usd", 365), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        client.RequestUris.Should().HaveCount(2);
    }

    [Fact]
    public async Task Should_FailAsAPlanRefusalWithNoCall_When_DaysExceedBasicsYearOfHistory()
    {
        var client = new FakeCoinMarketCapClient(_ => throw new InvalidOperationException("No call expected."));

        var result = await CreateService(client).GetMarketChartAsync(Query("bitcoin", "usd", 366), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.UpstreamUnavailable);
        result.ErrorDetail.Should().Contain("365");
        client.RequestUris.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnNotFoundAndMakeNoCall_When_TheCoinIdIsAnotherCoinsOverrideSlug()
    {
        var client = new FakeCoinMarketCapClient(_ => throw new InvalidOperationException("No call expected."));

        var result = await CreateService(client).GetMarketChartAsync(Query("xrp", "usd", 7), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        client.RequestUris.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnNotFoundWithNoHistoryCall_When_TheCoinIsMissingFromTheIdLookup()
    {
        var client = new FakeCoinMarketCapClient(Script(Latest()));

        var result = await CreateService(client).GetMarketChartAsync(Query("bitcoin", "usd", 7), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        client.RequestUris.Should().ContainSingle();
    }

    [Fact]
    public async Task Should_PassTheFailureOn_When_TheIdLookupFails()
    {
        var client = new FakeCoinMarketCapClient(Script(LatestFailure(HttpStatusCode.TooManyRequests)));

        var result = await CreateService(client).GetMarketChartAsync(Query("bitcoin", "usd", 7), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.RateLimited);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, ServiceErrorCode.RateLimited)]
    [InlineData(HttpStatusCode.Forbidden, ServiceErrorCode.UpstreamUnavailable)]
    [InlineData(HttpStatusCode.BadRequest, ServiceErrorCode.UpstreamUnavailable)]
    public async Task Should_FailWithTheMappedCode_When_TheHistoryCallFails(HttpStatusCode statusCode, ServiceErrorCode expected)
    {
        var client = new FakeCoinMarketCapClient(Script(
            Latest(Coin("bitcoin", 1, Quote("USD", 1m))),
            Failure<IReadOnlyDictionary<string, CoinMarketCapHistoricalCoin>>(statusCode)));

        var result = await CreateService(client).GetMarketChartAsync(Query("bitcoin", "usd", 7), CancellationToken.None);

        result.ErrorCode.Should().Be(expected);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_TheHistoryHoldsNoEntryForTheCoin()
    {
        var client = new FakeCoinMarketCapClient(Script(Latest(Coin("bitcoin", 1, Quote("USD", 1m))), Historical(2)));

        var result = await CreateService(client).GetMarketChartAsync(Query("bitcoin", "usd", 7), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_LeaveAPointOutOfOneSeries_When_ThatValueIsMissing()
    {
        var client = new FakeCoinMarketCapClient(Script(
            Latest(Coin("bitcoin", 1, Quote("USD", 1m))),
            Historical(1, Point("2026-11-26T23:59:59.999Z", "USD", 41000m, null, 31000m))));

        var result = await CreateService(client).GetMarketChartAsync(Query("bitcoin", "usd", 2), CancellationToken.None);

        var chart = ValueOf(result);
        chart.Prices.Should().ContainSingle();
        chart.MarketCaps.Should().BeEmpty();
        chart.TotalVolumes.Should().ContainSingle();
    }

    [Fact]
    public async Task Should_IgnoreAQuoteInAnotherCurrency_When_APointHoldsOne()
    {
        var client = new FakeCoinMarketCapClient(Script(
            Latest(Coin("bitcoin", 1, Quote("USD", 1m))),
            Historical(1, Point("2026-11-26T23:59:59.999Z", "EUR", 41000m, 810000m, 31000m))));

        var result = await CreateService(client).GetMarketChartAsync(Query("bitcoin", "usd", 2), CancellationToken.None);

        ValueOf(result).Prices.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_PassTheCallersCancellationToken_When_Calling()
    {
        using var cancellationSource = new CancellationTokenSource();
        var client = new FakeCoinMarketCapClient(Script(Latest(Coin("bitcoin", 1, Quote("USD", 1m))), Historical(1)));

        await CreateService(client).GetMarketChartAsync(Query("bitcoin", "usd", 7), cancellationSource.Token);

        client.CancellationTokens.Should().Equal(cancellationSource.Token, cancellationSource.Token);
    }

    // Upstream JSON: `required` rejects only a missing field, so an explicit
    // null still reaches the service at each of these points.

    public static TheoryData<string> HistoryNullPoints => new() { "history data", "coin entry" };

    [Theory]
    [MemberData(nameof(HistoryNullPoints))]
    public async Task Should_ReturnNotFound_When_TheHistoryIsNullAt(string point)
    {
        var client = new FakeCoinMarketCapClient(Script(Latest(Coin("bitcoin", 1, Quote("USD", 1m))), HistoryNullAt(point)));

        var result = await CreateService(client).GetMarketChartAsync(Query("bitcoin", "usd", 2), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    // `!` is deliberate: the case hands over the null upstream JSON can carry.
    private static CoinMarketCapResponse<IReadOnlyDictionary<string, CoinMarketCapHistoricalCoin>> HistoryNullAt(string point) => point switch
    {
        "history data" => CoinMarketCapResponse<IReadOnlyDictionary<string, CoinMarketCapHistoricalCoin>>.Success(null, HttpStatusCode.OK),
        "coin entry" => CoinMarketCapResponse<IReadOnlyDictionary<string, CoinMarketCapHistoricalCoin>>.Success(
            new Dictionary<string, CoinMarketCapHistoricalCoin> { ["1"] = null! }, HttpStatusCode.OK),
        _ => throw new ArgumentOutOfRangeException(nameof(point), point, "Not a null point the test knows."),
    };

    [Fact]
    public async Task Should_SkipNullPoints_When_TheHistoryHoldsThem()
    {
        // `!` is deliberate: the test hands over the nulls upstream JSON can carry.
        var points = new CoinMarketCapHistoricalQuote[]
        {
            null!,
            new() { Timestamp = Now, Quote = null! },
            new() { Timestamp = Now, Quote = new Dictionary<string, CoinMarketCapHistoricalCurrencyQuote> { ["USD"] = null! } },
            Point("2026-11-26T23:59:59.999Z", "USD", 41000m, 810000m, 31000m),
        };
        var client = new FakeCoinMarketCapClient(Script(Latest(Coin("bitcoin", 1, Quote("USD", 1m))), Historical(1, points)));

        var result = await CreateService(client).GetMarketChartAsync(Query("bitcoin", "usd", 2), CancellationToken.None);

        ValueOf(result).Prices.Should().ContainSingle();
    }

    [Fact]
    public async Task Should_ReturnEmptySeries_When_TheCoinsQuoteListIsNull()
    {
        // `!` is deliberate: the test hands over the null upstream JSON can carry.
        var historical = CoinMarketCapResponse<IReadOnlyDictionary<string, CoinMarketCapHistoricalCoin>>.Success(
            new Dictionary<string, CoinMarketCapHistoricalCoin> { ["1"] = new() { Quotes = null! } }, HttpStatusCode.OK);
        var client = new FakeCoinMarketCapClient(Script(Latest(Coin("bitcoin", 1, Quote("USD", 1m))), historical));

        var result = await CreateService(client).GetMarketChartAsync(Query("bitcoin", "usd", 2), CancellationToken.None);

        var chart = ValueOf(result);
        chart.Prices.Should().BeEmpty();
        chart.MarketCaps.Should().BeEmpty();
        chart.TotalVolumes.Should().BeEmpty();
    }
}
