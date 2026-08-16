using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;

namespace CryptoStuff.Core;

/// <summary>
/// A caching decorator over <see cref="ICryptocurrencyService"/>. Caches only
/// <see cref="GetCoinAsync"/> (keyed per coin) and
/// <see cref="GetDeveloperDataAsync"/> (keyed per coin and date) — prices and
/// historical series change too quickly to be worth caching, so those three
/// operations always pass straight through. Only successful results are
/// cached; a failure is retried on the very next request for the same key.
/// </summary>
public sealed class CachingCryptocurrencyService : ICryptocurrencyService, IDisposable
{
    private readonly ICryptocurrencyService _inner;
    private readonly TimeSpan _cacheDuration;
    private readonly MemoryCache _cache;

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cacheDuration"/> is not positive.</exception>
    public CachingCryptocurrencyService(ICryptocurrencyService inner, TimeSpan cacheDuration, TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cacheDuration, TimeSpan.Zero);

        _inner = inner;
        _cacheDuration = cacheDuration;
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            // MemoryCacheOptions has no TimeProvider property in this package
            // version — only the older ISystemClock. Bridging to it keeps the
            // public constructor on TimeProvider, per this repo's convention.
            Clock = new SystemClockAdapter(timeProvider ?? TimeProvider.System),
        });
    }

    /// <inheritdoc />
    public Task<ServiceResult<CoinPriceView>> GetPricesAsync(CoinPriceQuery query, CancellationToken cancellationToken) =>
        _inner.GetPricesAsync(query, cancellationToken);

    /// <inheritdoc />
    public Task<ServiceResult<TokenPriceView>> GetTokenPricesAsync(TokenPriceQuery query, CancellationToken cancellationToken) =>
        _inner.GetTokenPricesAsync(query, cancellationToken);

    /// <inheritdoc />
    public Task<ServiceResult<CoinMarketChartView>> GetMarketChartAsync(CoinMarketChartQuery query, CancellationToken cancellationToken) =>
        _inner.GetMarketChartAsync(query, cancellationToken);

    /// <inheritdoc />
    public Task<ServiceResult<CoinView>> GetCoinAsync(CoinQuery query, CancellationToken cancellationToken) =>
        GetOrAddAsync(new CoinCacheKey(query.CoinId), () => _inner.GetCoinAsync(query, cancellationToken));

    /// <inheritdoc />
    public Task<ServiceResult<CoinDeveloperDataView>> GetDeveloperDataAsync(CoinDeveloperDataQuery query, CancellationToken cancellationToken) =>
        GetOrAddAsync(new DeveloperDataCacheKey(query.CoinId, query.Date), () => _inner.GetDeveloperDataAsync(query, cancellationToken));

    /// <summary>Disposes the underlying cache.</summary>
    public void Dispose() => _cache.Dispose();

    /// <summary>
    /// Concurrent misses on the same key are not deduplicated — each caller
    /// independently calls <paramref name="fetch"/> and (on success) writes
    /// its own cache entry. Acceptable here: a miss only ever happens once
    /// per key per <see cref="_cacheDuration"/> window in practice, and the
    /// roadmap never asked for request-coalescing.
    /// </summary>
    private async Task<ServiceResult<TView>> GetOrAddAsync<TKey, TView>(TKey key, Func<Task<ServiceResult<TView>>> fetch)
        where TKey : notnull
    {
        if (_cache.TryGetValue<ServiceResult<TView>>(key, out var cached))
        {
            // Only ever set below from a non-null successful result, so this
            // cannot itself be null.
            return cached!;
        }

        var result = await fetch();
        if (result.IsSuccess)
        {
            _cache.Set(key, result, _cacheDuration);
        }

        return result;
    }

    private readonly record struct CoinCacheKey(string CoinId);

    private readonly record struct DeveloperDataCacheKey(string CoinId, string Date);

    private sealed class SystemClockAdapter(TimeProvider timeProvider) : ISystemClock
    {
        public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
    }
}
