using CryptoStuff.Core.UnitTests.TestSupport;
using static CryptoStuff.Core.UnitTests.TestSupport.PriceResults;

namespace CryptoStuff.Core.UnitTests;

public class KnownCoinPricesTests
{
    private static CoinPriceQuery Query(string[] coinIds, params string[] vsCurrencies) =>
        new() { CoinIds = coinIds, VsCurrencies = vsCurrencies };

    private static CoinPrice NoAmounts(string coinId) => new() { CoinId = coinId, Amounts = [] };

    // Asserts the result succeeded and returns what it carries.
    private static CoinPriceView SucceededValue(ServiceResult<CoinPriceView> result)
    {
        result.IsSuccess.Should().BeTrue();

        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        return result.Value!;
    }

    [Fact]
    public async Task Should_ReturnTheOneCallsPricesWithoutAskingAgain_When_UpstreamKnowsEveryCoin()
    {
        var upstream = new SpyCryptocurrencyService { PricesResult = Prices(("bitcoin", "usd", 1m), ("ethereum", "usd", 2m)) };

        var result = await upstream.GetKnownCoinPricesAsync(Query(["bitcoin", "ethereum"], "usd"), CancellationToken.None);

        SucceededValue(result).Should().BeEquivalentTo(Prices(("bitcoin", "usd", 1m), ("ethereum", "usd", 2m)).Value);
        upstream.GetPricesCallCount.Should().Be(1);
    }

    [Theory]
    [InlineData(ServiceErrorCode.RateLimited)]
    [InlineData(ServiceErrorCode.RequestTimedOut)]
    [InlineData(ServiceErrorCode.UpstreamUnavailable)]
    public async Task Should_PassTheFailureThroughWithoutAskingAgain_When_TheOneCallFailsOtherwise(ServiceErrorCode errorCode)
    {
        var upstream = new SpyCryptocurrencyService { PricesResult = ServiceResult<CoinPriceView>.Failure(errorCode, "upstream said no") };

        var result = await upstream.GetKnownCoinPricesAsync(Query(["bitcoin", "ethereum"], "usd"), CancellationToken.None);

        result.ErrorCode.Should().Be(errorCode);
        result.ErrorDetail.Should().Be("upstream said no");
        upstream.GetPricesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_PriceKnownCoinsAndListUnknownOnesWithoutAmountsInQueryOrder_When_UpstreamDoesNotKnowACoin()
    {
        var upstream = new SpyCryptocurrencyService
        {
            PricesResultFor = KnowsOnly(("bitcoin", "usd", 1m), ("bitcoin", "eur", 3m), ("ethereum", "usd", 2m)),
        };

        var result = await upstream.GetKnownCoinPricesAsync(Query(["bitcoin", "gone-coin", "ethereum"], "usd", "eur"), CancellationToken.None);

        // `!` is safe: Prices always builds a successful result, which always
        // carries its value.
        SucceededValue(result).Coins.Should().BeEquivalentTo(
            [
                .. Prices(("bitcoin", "usd", 1m), ("bitcoin", "eur", 3m)).Value!.Coins,
                NoAmounts("gone-coin"),
                .. Prices(("ethereum", "usd", 2m)).Value!.Coins,
            ],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Should_AskForEachCoinOnItsOwnInEveryCurrency_When_TheOneCallNamesAnUnknownCoin()
    {
        var upstream = new SpyCryptocurrencyService { PricesResultFor = KnowsOnly(("bitcoin", "usd", 1m)) };

        await upstream.GetKnownCoinPricesAsync(Query(["bitcoin", "gone-coin"], "usd", "eur"), CancellationToken.None);

        upstream.CapturedPricesQueries
            .Select(q => $"{string.Join(",", q.CoinIds)} in {string.Join(",", q.VsCurrencies)}")
            .Should().Equal("bitcoin,gone-coin in usd,eur", "bitcoin in usd,eur", "gone-coin in usd,eur");
    }

    [Fact]
    public async Task Should_ListTheCoinWithoutAmountsWithoutAskingAgain_When_TheOnlyCoinIsUnknown()
    {
        var upstream = new SpyCryptocurrencyService { PricesResult = ServiceResult<CoinPriceView>.Failure(ServiceErrorCode.NotFound) };

        var result = await upstream.GetKnownCoinPricesAsync(Query(["gone-coin"], "usd"), CancellationToken.None);

        SucceededValue(result).Coins.Should().BeEquivalentTo([NoAmounts("gone-coin")]);
        upstream.GetPricesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_ListEveryCoinWithoutAmounts_When_UpstreamKnowsNoneOfTheCoins()
    {
        var upstream = new SpyCryptocurrencyService { PricesResult = ServiceResult<CoinPriceView>.Failure(ServiceErrorCode.NotFound) };

        var result = await upstream.GetKnownCoinPricesAsync(Query(["gone-coin", "lost-coin"], "usd"), CancellationToken.None);

        SucceededValue(result).Coins.Should().BeEquivalentTo([NoAmounts("gone-coin"), NoAmounts("lost-coin")], options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Should_ReturnNoCoinsWithoutAskingAgain_When_TheQueryNamesNoCoinsAndTheOneCallIsNotFound()
    {
        var upstream = new SpyCryptocurrencyService { PricesResult = ServiceResult<CoinPriceView>.Failure(ServiceErrorCode.NotFound) };

        var result = await upstream.GetKnownCoinPricesAsync(Query([], "usd"), CancellationToken.None);

        SucceededValue(result).Coins.Should().BeEmpty();
        upstream.GetPricesCallCount.Should().Be(1);
    }

    [Theory]
    [InlineData(ServiceErrorCode.RateLimited)]
    [InlineData(ServiceErrorCode.RequestTimedOut)]
    [InlineData(ServiceErrorCode.UpstreamUnavailable)]
    public async Task Should_PassTheFailureThrough_When_AskingForOneCoinFails(ServiceErrorCode errorCode)
    {
        var upstream = new SpyCryptocurrencyService
        {
            PricesResultFor = query => query.CoinIds switch
            {
                ["bitcoin"] => Prices(("bitcoin", "usd", 1m)),
                ["ethereum"] => ServiceResult<CoinPriceView>.Failure(errorCode, "upstream said no"),
                _ => ServiceResult<CoinPriceView>.Failure(ServiceErrorCode.NotFound),
            },
        };

        var result = await upstream.GetKnownCoinPricesAsync(Query(["bitcoin", "ethereum", "gone-coin"], "usd"), CancellationToken.None);

        result.ErrorCode.Should().Be(errorCode);
        result.ErrorDetail.Should().Be("upstream said no");
    }

    [Fact]
    public async Task Should_PassTheCancellationTokenToEveryCall_When_AskingCoinByCoin()
    {
        var upstream = new SpyCryptocurrencyService();
        var tokens = new List<CancellationToken>();
        var knowsBitcoin = KnowsOnly(("bitcoin", "usd", 1m));
        upstream.PricesResultFor = query =>
        {
            tokens.Add(upstream.CapturedCancellationToken);
            return knowsBitcoin(query);
        };
        using var source = new CancellationTokenSource();

        await upstream.GetKnownCoinPricesAsync(Query(["bitcoin", "gone-coin"], "usd"), source.Token);

        tokens.Should().HaveCount(3).And.AllSatisfy(token => token.Should().Be(source.Token));
    }

    [Fact]
    public async Task Should_PropagateCancellation_When_AnUpstreamCallIsCancelled()
    {
        var upstream = new SpyCryptocurrencyService { ExceptionToThrow = new OperationCanceledException() };

        var act = () => upstream.GetKnownCoinPricesAsync(Query(["bitcoin"], "usd"), CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
