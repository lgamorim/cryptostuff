using CryptoStuff.Core.UnitTests.TestSupport;

namespace CryptoStuff.Core.UnitTests;

public class NegativePriceGuardTests
{
    private static readonly CoinPriceQuery PriceQuery = new() { CoinIds = ["bitcoin", "ethereum"], VsCurrencies = ["usd", "eur", "btc"] };

    private static readonly TokenPriceQuery TokenQuery = new()
    {
        Platform = "ethereum",
        ContractAddresses = ["0xabc", "0xdef"],
        VsCurrencies = ["usd", "eur", "btc"],
    };

    private static (SpyCryptocurrencyService Inner, NegativePriceGuard Guard) CreateSut()
    {
        var inner = new SpyCryptocurrencyService();
        return (inner, new NegativePriceGuard(inner));
    }

    private static CurrencyAmount Amount(string currency, decimal amount) => new() { Currency = currency, Amount = amount };

    // GetPricesAsync.

    [Fact]
    public async Task Should_DropOnlyTheNegativeAmount_When_GetPricesAsyncGetsACoinPricedBelowZeroInOneCurrency()
    {
        var (inner, guard) = CreateSut();
        inner.PricesResult = ServiceResult<CoinPriceView>.Success(new CoinPriceView
        {
            Coins = [new CoinPrice { CoinId = "bitcoin", Amounts = [Amount("usd", 43000m), Amount("eur", -1m), Amount("btc", 1m)] }],
        });

        var result = await guard.GetPricesAsync(PriceQuery, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // `!` is safe: a successful result always carries its value.
        result.Value!.Coins.Should().BeEquivalentTo(
            [new CoinPrice { CoinId = "bitcoin", Amounts = [Amount("usd", 43000m), Amount("btc", 1m)] }],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Should_KeepTheCoinWithoutAmounts_When_GetPricesAsyncGetsACoinPricedBelowZeroInEveryCurrency()
    {
        var (inner, guard) = CreateSut();
        inner.PricesResult = ServiceResult<CoinPriceView>.Success(new CoinPriceView
        {
            Coins =
            [
                new CoinPrice { CoinId = "bitcoin", Amounts = [Amount("usd", 43000m)] },
                new CoinPrice { CoinId = "ethereum", Amounts = [Amount("usd", -0.01m)] },
            ],
        });

        var result = await guard.GetPricesAsync(PriceQuery, CancellationToken.None);

        // `!` is safe: a successful result always carries its value.
        result.Value!.Coins.Should().BeEquivalentTo(
            [
                new CoinPrice { CoinId = "bitcoin", Amounts = [Amount("usd", 43000m)] },
                new CoinPrice { CoinId = "ethereum", Amounts = [] },
            ],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Should_KeepZeroAndPositiveAmounts_When_GetPricesAsyncGetsNoPriceBelowZero()
    {
        var (inner, guard) = CreateSut();
        inner.PricesResult = ServiceResult<CoinPriceView>.Success(new CoinPriceView
        {
            Coins = [new CoinPrice { CoinId = "bitcoin", Amounts = [Amount("usd", 0m), Amount("eur", 0.000001m)] }],
        });

        var result = await guard.GetPricesAsync(PriceQuery, CancellationToken.None);

        // `!` is safe: a successful result always carries its value.
        result.Value!.Coins.Should().BeEquivalentTo(
            [new CoinPrice { CoinId = "bitcoin", Amounts = [Amount("usd", 0m), Amount("eur", 0.000001m)] }],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Should_ReturnNoCoins_When_GetPricesAsyncGetsNoCoins()
    {
        var (inner, guard) = CreateSut();
        inner.PricesResult = ServiceResult<CoinPriceView>.Success(new CoinPriceView { Coins = [] });

        var result = await guard.GetPricesAsync(PriceQuery, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // `!` is safe: a successful result always carries its value.
        result.Value!.Coins.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_KeepTheCoinWithoutAmounts_When_GetPricesAsyncGetsACoinWithNoAmounts()
    {
        var (inner, guard) = CreateSut();
        inner.PricesResult = ServiceResult<CoinPriceView>.Success(new CoinPriceView
        {
            Coins = [new CoinPrice { CoinId = "bitcoin", Amounts = [] }],
        });

        var result = await guard.GetPricesAsync(PriceQuery, CancellationToken.None);

        // `!` is safe: a successful result always carries its value.
        result.Value!.Coins.Should().BeEquivalentTo([new CoinPrice { CoinId = "bitcoin", Amounts = [] }]);
    }

    [Theory]
    [InlineData(ServiceErrorCode.NotFound)]
    [InlineData(ServiceErrorCode.UpstreamUnavailable)]
    public async Task Should_PassTheFailureThroughUnchanged_When_GetPricesAsyncFails(ServiceErrorCode errorCode)
    {
        var (inner, guard) = CreateSut();
        inner.PricesResult = ServiceResult<CoinPriceView>.Failure(errorCode, "A specific explanation.");

        var result = await guard.GetPricesAsync(PriceQuery, CancellationToken.None);

        result.Should().Be(inner.PricesResult);
    }

    [Fact]
    public async Task Should_PropagateTheException_When_GetPricesAsyncInnerThrows()
    {
        var (inner, guard) = CreateSut();
        inner.ExceptionToThrow = new InvalidOperationException("upstream boom");

        var act = () => guard.GetPricesAsync(PriceQuery, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage("upstream boom");
    }

    [Fact]
    public async Task Should_PropagateTheCancellation_When_GetPricesAsyncInnerIsCanceled()
    {
        var (inner, guard) = CreateSut();
        inner.ExceptionToThrow = new OperationCanceledException();

        var act = () => guard.GetPricesAsync(PriceQuery, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ForwardTheQueryAndCancellationToken_When_GetPricesAsyncCalled()
    {
        var (inner, guard) = CreateSut();
        using var cancellationTokenSource = new CancellationTokenSource();

        await guard.GetPricesAsync(PriceQuery, cancellationTokenSource.Token);

        inner.CapturedPricesQuery.Should().Be(PriceQuery);
        inner.CapturedCancellationToken.Should().Be(cancellationTokenSource.Token);
    }

    // GetTokenPricesAsync.

    [Fact]
    public async Task Should_DropOnlyTheNegativeAmounts_When_GetTokenPricesAsyncGetsATokenPricedBelowZero()
    {
        var (inner, guard) = CreateSut();
        inner.TokenPricesResult = ServiceResult<TokenPriceView>.Success(new TokenPriceView
        {
            Tokens =
            [
                new TokenPrice { ContractAddress = "0xabc", Amounts = [Amount("usd", -5m), Amount("eur", 0m), Amount("btc", 0.0001m)] },
                new TokenPrice { ContractAddress = "0xdef", Amounts = [Amount("usd", -1m)] },
            ],
        });

        var result = await guard.GetTokenPricesAsync(TokenQuery, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // `!` is safe: a successful result always carries its value.
        result.Value!.Tokens.Should().BeEquivalentTo(
            [
                new TokenPrice { ContractAddress = "0xabc", Amounts = [Amount("eur", 0m), Amount("btc", 0.0001m)] },
                new TokenPrice { ContractAddress = "0xdef", Amounts = [] },
            ],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Should_ReturnNoTokens_When_GetTokenPricesAsyncGetsNoTokens()
    {
        var (inner, guard) = CreateSut();
        inner.TokenPricesResult = ServiceResult<TokenPriceView>.Success(new TokenPriceView { Tokens = [] });

        var result = await guard.GetTokenPricesAsync(TokenQuery, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // `!` is safe: a successful result always carries its value.
        result.Value!.Tokens.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_PassTheFailureThroughUnchanged_When_GetTokenPricesAsyncFails()
    {
        var (inner, guard) = CreateSut();
        inner.TokenPricesResult = ServiceResult<TokenPriceView>.Failure(ServiceErrorCode.NotFound, "A specific explanation.");

        var result = await guard.GetTokenPricesAsync(TokenQuery, CancellationToken.None);

        result.Should().Be(inner.TokenPricesResult);
    }

    [Fact]
    public async Task Should_PropagateTheException_When_GetTokenPricesAsyncInnerThrows()
    {
        var (inner, guard) = CreateSut();
        inner.ExceptionToThrow = new InvalidOperationException("upstream boom");

        var act = () => guard.GetTokenPricesAsync(TokenQuery, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage("upstream boom");
    }

    [Fact]
    public async Task Should_PropagateTheCancellation_When_GetTokenPricesAsyncInnerIsCanceled()
    {
        var (inner, guard) = CreateSut();
        inner.ExceptionToThrow = new OperationCanceledException();

        var act = () => guard.GetTokenPricesAsync(TokenQuery, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ForwardTheQueryAndCancellationToken_When_GetTokenPricesAsyncCalled()
    {
        var (inner, guard) = CreateSut();
        using var cancellationTokenSource = new CancellationTokenSource();

        await guard.GetTokenPricesAsync(TokenQuery, cancellationTokenSource.Token);

        inner.CapturedTokenPricesQuery.Should().Be(TokenQuery);
        inner.CapturedCancellationToken.Should().Be(cancellationTokenSource.Token);
    }

    // The operations that are shown, not computed from, pass through untouched.

    [Fact]
    public async Task Should_ReturnTheInnerResultUntouched_When_GetMarketChartAsyncGetsAPointBelowZero()
    {
        var (inner, guard) = CreateSut();
        inner.MarketChartResult = ServiceResult<CoinMarketChartView>.Success(new CoinMarketChartView
        {
            Prices = [new CoinMarketChartPoint { Date = "2024-01-31", Value = -1m }],
            MarketCaps = [],
            TotalVolumes = [],
        });
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await guard.GetMarketChartAsync(
            new CoinMarketChartQuery { CoinId = "bitcoin", VsCurrency = "usd", Days = 7 }, cancellationTokenSource.Token);

        result.Should().BeSameAs(inner.MarketChartResult);
        inner.CapturedCancellationToken.Should().Be(cancellationTokenSource.Token);
    }

    [Fact]
    public async Task Should_ReturnTheInnerResultUntouched_When_GetCoinAsyncGetsASnapshotPricedBelowZero()
    {
        var (inner, guard) = CreateSut();
        inner.CoinResult = ServiceResult<CoinView>.Success(new CoinView
        {
            Id = "bitcoin",
            Symbol = "btc",
            Name = "Bitcoin",
            MarketData = [new CoinCurrencySnapshot { Currency = "usd", Price = -1m, MarketCap = 0m, Volume = 0m }],
        });
        var query = new CoinQuery { CoinId = "bitcoin" };
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await guard.GetCoinAsync(query, cancellationTokenSource.Token);

        result.Should().BeSameAs(inner.CoinResult);
        inner.CapturedCoinQuery.Should().Be(query);
        inner.CapturedCancellationToken.Should().Be(cancellationTokenSource.Token);
    }

    [Fact]
    public async Task Should_ReturnTheInnerResultUntouched_When_GetDeveloperDataAsyncCalled()
    {
        var (inner, guard) = CreateSut();
        inner.DeveloperDataResult = ServiceResult<CoinDeveloperDataView>.Failure(ServiceErrorCode.Unsupported, "A specific explanation.");
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await guard.GetDeveloperDataAsync(
            new CoinDeveloperDataQuery { CoinId = "bitcoin", Date = "31-01-2024" }, cancellationTokenSource.Token);

        result.Should().BeSameAs(inner.DeveloperDataResult);
        inner.CapturedCancellationToken.Should().Be(cancellationTokenSource.Token);
    }
}
