using CryptoStuff.Core.UnitTests.TestSupport;

namespace CryptoStuff.Core.UnitTests;

public sealed class CachingCryptocurrencyServiceMetricsTests : IDisposable
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly SpyCryptocurrencyService _inner = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);
    private readonly RecordingMeterFactory _meterFactory = new();
    private readonly CachingCryptocurrencyService _decorator;

    public CachingCryptocurrencyServiceMetricsTests() =>
        _decorator = new CachingCryptocurrencyService(_inner, CacheDuration, _clock, _meterFactory);

    public void Dispose()
    {
        _decorator.Dispose();
        _meterFactory.Dispose();
    }

    private static RecordedMeasurement Lookup(string operation, string result) => new(
        "cryptostuff.upstream",
        "cryptostuff.cache.lookups",
        "{lookup}",
        1,
        new Dictionary<string, object?> { ["operation"] = operation, ["result"] = result });

    [Fact]
    public async Task Should_RecordOneMiss_When_GetCoinAsyncCalledFirstTimeForACoin()
    {
        var query = new CoinQuery { CoinId = "bitcoin" };

        await _decorator.GetCoinAsync(query, CancellationToken.None);

        _meterFactory.Measurements.Should().BeEquivalentTo([Lookup("get_coin", "miss")]);
    }

    [Fact]
    public async Task Should_RecordMissThenHit_When_GetCoinAsyncCalledTwiceForSameCoinWithinTtl()
    {
        var query = new CoinQuery { CoinId = "bitcoin" };

        await _decorator.GetCoinAsync(query, CancellationToken.None);
        await _decorator.GetCoinAsync(query, CancellationToken.None);

        _meterFactory.Measurements.Should().BeEquivalentTo(
            [Lookup("get_coin", "miss"), Lookup("get_coin", "hit")],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Should_TagLookupsAsDeveloperData_When_GetDeveloperDataAsyncCalledTwiceForSameCoinAndDate()
    {
        var query = new CoinDeveloperDataQuery { CoinId = "bitcoin", Date = "30-12-2024" };

        await _decorator.GetDeveloperDataAsync(query, CancellationToken.None);
        await _decorator.GetDeveloperDataAsync(query, CancellationToken.None);

        _meterFactory.Measurements.Should().BeEquivalentTo(
            [Lookup("get_developer_data", "miss"), Lookup("get_developer_data", "hit")],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Should_RecordSecondMiss_When_GetCoinAsyncCacheEntryHasExpired()
    {
        var query = new CoinQuery { CoinId = "bitcoin" };

        await _decorator.GetCoinAsync(query, CancellationToken.None);
        _clock.Advance(CacheDuration + TimeSpan.FromSeconds(1));
        await _decorator.GetCoinAsync(query, CancellationToken.None);

        _meterFactory.Measurements.Should().BeEquivalentTo([Lookup("get_coin", "miss"), Lookup("get_coin", "miss")]);
    }

    [Fact]
    public async Task Should_RecordSecondMiss_When_FirstGetCoinAsyncResultWasAFailure()
    {
        _inner.CoinResult = ServiceResult<CoinView>.Failure(ServiceErrorCode.UpstreamUnavailable);
        var query = new CoinQuery { CoinId = "bitcoin" };

        await _decorator.GetCoinAsync(query, CancellationToken.None);
        await _decorator.GetCoinAsync(query, CancellationToken.None);

        _meterFactory.Measurements.Should().BeEquivalentTo([Lookup("get_coin", "miss"), Lookup("get_coin", "miss")]);
    }

    [Fact]
    public async Task Should_RecordMiss_When_InnerServiceThrows()
    {
        _inner.ExceptionToThrow = new OperationCanceledException();
        var query = new CoinQuery { CoinId = "bitcoin" };

        var act = () => _decorator.GetCoinAsync(query, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _meterFactory.Measurements.Should().BeEquivalentTo([Lookup("get_coin", "miss")]);
    }

    [Fact]
    public async Task Should_RecordNothing_When_OnlyUncachedOperationsAreCalled()
    {
        await _decorator.GetPricesAsync(
            new CoinPriceQuery { CoinIds = ["bitcoin"], VsCurrencies = ["usd"] }, CancellationToken.None);
        await _decorator.GetTokenPricesAsync(
            new TokenPriceQuery { Platform = "ethereum", ContractAddresses = ["0xabc"], VsCurrencies = ["usd"] }, CancellationToken.None);
        await _decorator.GetMarketChartAsync(
            new CoinMarketChartQuery { CoinId = "bitcoin", VsCurrency = "usd", Days = 1 }, CancellationToken.None);

        _meterFactory.Measurements.Should().BeEmpty();
    }
}
