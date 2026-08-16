using CryptoStuff.Core.UnitTests.TestSupport;

namespace CryptoStuff.Core.UnitTests;

public class CachingCryptocurrencyServiceTests
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private static (SpyCryptocurrencyService Inner, FakeTimeProvider Clock, CachingCryptocurrencyService Decorator) CreateSut()
    {
        var inner = new SpyCryptocurrencyService();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var decorator = new CachingCryptocurrencyService(inner, CacheDuration, clock);
        return (inner, clock, decorator);
    }

    // Constructor validation.

    [Fact]
    public void Should_ThrowArgumentOutOfRangeException_When_CacheDurationIsZero()
    {
        var act = () => new CachingCryptocurrencyService(new SpyCryptocurrencyService(), TimeSpan.Zero);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Should_ThrowArgumentOutOfRangeException_When_CacheDurationIsNegative()
    {
        var act = () => new CachingCryptocurrencyService(new SpyCryptocurrencyService(), TimeSpan.FromSeconds(-1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // GetCoinAsync — cached per coin.

    [Fact]
    public async Task Should_ReturnInnerResult_When_GetCoinAsyncCalledFirstTimeForACoin()
    {
        var (inner, _, decorator) = CreateSut();
        var query = new CoinQuery { CoinId = "bitcoin" };

        var result = await decorator.GetCoinAsync(query, CancellationToken.None);

        result.Should().Be(inner.CoinResult);
    }

    [Fact]
    public async Task Should_NotCallInner_When_GetCoinAsyncCalledTwiceForSameCoinWithinTtl()
    {
        var (inner, _, decorator) = CreateSut();
        var query = new CoinQuery { CoinId = "bitcoin" };

        await decorator.GetCoinAsync(query, CancellationToken.None);
        await decorator.GetCoinAsync(query, CancellationToken.None);

        inner.GetCoinCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_CallInnerAgain_When_GetCoinAsyncCacheEntryExpiresAfterConfiguredDuration()
    {
        var (inner, clock, decorator) = CreateSut();
        var query = new CoinQuery { CoinId = "bitcoin" };

        await decorator.GetCoinAsync(query, CancellationToken.None);
        clock.Advance(CacheDuration + TimeSpan.FromSeconds(1));
        await decorator.GetCoinAsync(query, CancellationToken.None);

        inner.GetCoinCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Should_NotCacheFailure_When_GetCoinAsyncInnerReturnsFailure()
    {
        var (inner, _, decorator) = CreateSut();
        inner.CoinResult = ServiceResult<CoinView>.Failure(ServiceErrorCode.NotFound);
        var query = new CoinQuery { CoinId = "bitcoin" };

        await decorator.GetCoinAsync(query, CancellationToken.None);
        await decorator.GetCoinAsync(query, CancellationToken.None);

        inner.GetCoinCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Should_CacheIndependently_When_GetCoinAsyncCalledForDifferentCoinIds()
    {
        var (inner, _, decorator) = CreateSut();

        await decorator.GetCoinAsync(new CoinQuery { CoinId = "bitcoin" }, CancellationToken.None);
        await decorator.GetCoinAsync(new CoinQuery { CoinId = "ethereum" }, CancellationToken.None);

        inner.GetCoinCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Should_ForwardCancellationToken_When_GetCoinAsyncCacheMisses()
    {
        var (inner, _, decorator) = CreateSut();
        using var cts = new CancellationTokenSource();

        await decorator.GetCoinAsync(new CoinQuery { CoinId = "bitcoin" }, cts.Token);

        inner.CapturedCancellationToken.Should().Be(cts.Token);
    }

    // GetDeveloperDataAsync — cached per coin and date.

    [Fact]
    public async Task Should_ReturnInnerResult_When_GetDeveloperDataAsyncCalledFirstTimeForACoinAndDate()
    {
        var (inner, _, decorator) = CreateSut();
        var query = new CoinDeveloperDataQuery { CoinId = "bitcoin", Date = "01-01-2024" };

        var result = await decorator.GetDeveloperDataAsync(query, CancellationToken.None);

        result.Should().Be(inner.DeveloperDataResult);
    }

    [Fact]
    public async Task Should_NotCallInner_When_GetDeveloperDataAsyncCalledTwiceForSameCoinAndDateWithinTtl()
    {
        var (inner, _, decorator) = CreateSut();
        var query = new CoinDeveloperDataQuery { CoinId = "bitcoin", Date = "01-01-2024" };

        await decorator.GetDeveloperDataAsync(query, CancellationToken.None);
        await decorator.GetDeveloperDataAsync(query, CancellationToken.None);

        inner.GetDeveloperDataCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_CallInnerAgain_When_GetDeveloperDataAsyncCacheEntryExpiresAfterConfiguredDuration()
    {
        var (inner, clock, decorator) = CreateSut();
        var query = new CoinDeveloperDataQuery { CoinId = "bitcoin", Date = "01-01-2024" };

        await decorator.GetDeveloperDataAsync(query, CancellationToken.None);
        clock.Advance(CacheDuration + TimeSpan.FromSeconds(1));
        await decorator.GetDeveloperDataAsync(query, CancellationToken.None);

        inner.GetDeveloperDataCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Should_NotCacheFailure_When_GetDeveloperDataAsyncInnerReturnsFailure()
    {
        var (inner, _, decorator) = CreateSut();
        inner.DeveloperDataResult = ServiceResult<CoinDeveloperDataView>.Failure(ServiceErrorCode.NotFound);
        var query = new CoinDeveloperDataQuery { CoinId = "bitcoin", Date = "01-01-2024" };

        await decorator.GetDeveloperDataAsync(query, CancellationToken.None);
        await decorator.GetDeveloperDataAsync(query, CancellationToken.None);

        inner.GetDeveloperDataCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Should_CacheIndependently_When_GetDeveloperDataAsyncCalledForSameCoinButDifferentDates()
    {
        var (inner, _, decorator) = CreateSut();

        await decorator.GetDeveloperDataAsync(new CoinDeveloperDataQuery { CoinId = "bitcoin", Date = "01-01-2024" }, CancellationToken.None);
        await decorator.GetDeveloperDataAsync(new CoinDeveloperDataQuery { CoinId = "bitcoin", Date = "02-01-2024" }, CancellationToken.None);

        inner.GetDeveloperDataCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Should_CacheIndependently_When_GetDeveloperDataAsyncCalledForDifferentCoinsSameDate()
    {
        var (inner, _, decorator) = CreateSut();

        await decorator.GetDeveloperDataAsync(new CoinDeveloperDataQuery { CoinId = "bitcoin", Date = "01-01-2024" }, CancellationToken.None);
        await decorator.GetDeveloperDataAsync(new CoinDeveloperDataQuery { CoinId = "ethereum", Date = "01-01-2024" }, CancellationToken.None);

        inner.GetDeveloperDataCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Should_ForwardCancellationToken_When_GetDeveloperDataAsyncCacheMisses()
    {
        var (inner, _, decorator) = CreateSut();
        using var cts = new CancellationTokenSource();

        await decorator.GetDeveloperDataAsync(new CoinDeveloperDataQuery { CoinId = "bitcoin", Date = "01-01-2024" }, cts.Token);

        inner.CapturedCancellationToken.Should().Be(cts.Token);
    }

    // Cross-key collision safety.

    [Fact]
    public async Task Should_NotShareCacheEntries_When_GetCoinAsyncAndGetDeveloperDataAsyncCalledWithSameCoinId()
    {
        var (inner, _, decorator) = CreateSut();

        await decorator.GetCoinAsync(new CoinQuery { CoinId = "bitcoin" }, CancellationToken.None);
        await decorator.GetDeveloperDataAsync(new CoinDeveloperDataQuery { CoinId = "bitcoin", Date = "01-01-2024" }, CancellationToken.None);

        inner.GetCoinCallCount.Should().Be(1);
        inner.GetDeveloperDataCallCount.Should().Be(1);
    }

    // Uncached passthrough.

    [Fact]
    public async Task Should_AlwaysCallInner_When_GetPricesAsyncCalledRepeatedlyWithIdenticalQuery()
    {
        var (inner, _, decorator) = CreateSut();
        var query = new CoinPriceQuery { CoinIds = ["bitcoin"], VsCurrencies = ["usd"] };

        await decorator.GetPricesAsync(query, CancellationToken.None);
        await decorator.GetPricesAsync(query, CancellationToken.None);

        inner.GetPricesCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Should_AlwaysCallInner_When_GetTokenPricesAsyncCalledRepeatedlyWithIdenticalQuery()
    {
        var (inner, _, decorator) = CreateSut();
        var query = new TokenPriceQuery { Platform = "ethereum", ContractAddresses = ["0xaaa"], VsCurrencies = ["usd"] };

        await decorator.GetTokenPricesAsync(query, CancellationToken.None);
        await decorator.GetTokenPricesAsync(query, CancellationToken.None);

        inner.GetTokenPricesCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Should_AlwaysCallInner_When_GetMarketChartAsyncCalledRepeatedlyWithIdenticalQuery()
    {
        var (inner, _, decorator) = CreateSut();
        var query = new CoinMarketChartQuery { CoinId = "bitcoin", VsCurrency = "usd", Days = 1 };

        await decorator.GetMarketChartAsync(query, CancellationToken.None);
        await decorator.GetMarketChartAsync(query, CancellationToken.None);

        inner.GetMarketChartCallCount.Should().Be(2);
    }

    // Failure paths.

    [Fact]
    public async Task Should_PropagateExceptionWithoutCaching_When_GetCoinAsyncInnerThrows()
    {
        var (inner, _, decorator) = CreateSut();
        inner.ExceptionToThrow = new InvalidOperationException("upstream boom");
        var query = new CoinQuery { CoinId = "bitcoin" };

        var firstAct = async () => await decorator.GetCoinAsync(query, CancellationToken.None);
        await firstAct.Should().ThrowAsync<InvalidOperationException>();

        var secondAct = async () => await decorator.GetCoinAsync(query, CancellationToken.None);
        await secondAct.Should().ThrowAsync<InvalidOperationException>();
        inner.GetCoinCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Should_ReturnCachedResult_When_GetCoinAsyncCacheHitCalledWithAlreadyCancelledToken()
    {
        var (inner, _, decorator) = CreateSut();
        var query = new CoinQuery { CoinId = "bitcoin" };
        await decorator.GetCoinAsync(query, CancellationToken.None);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await decorator.GetCoinAsync(query, cts.Token);

        result.Should().Be(inner.CoinResult);
        inner.GetCoinCallCount.Should().Be(1);
    }

    // Lifecycle.

    [Fact]
    public async Task Should_ThrowObjectDisposedException_When_GetCoinAsyncCalledAfterDispose()
    {
        var (_, _, decorator) = CreateSut();
        decorator.Dispose();

        var act = async () => await decorator.GetCoinAsync(new CoinQuery { CoinId = "bitcoin" }, CancellationToken.None);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }
}
