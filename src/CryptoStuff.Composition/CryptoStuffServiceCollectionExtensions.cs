using System.Globalization;
using CryptoStuff.CoinGecko;
using CryptoStuff.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoStuff.Composition;

/// <summary>
/// Registers everything <c>CryptoStuff</c> needs to serve cryptocurrency data:
/// the CoinGecko-backed <see cref="ICryptocurrencyService"/>, wrapped in its
/// caching decorator, wired to a CoinGecko <see cref="HttpClient"/> with the
/// rate-limit retry handler attached.
/// </summary>
public static class CryptoStuffServiceCollectionExtensions
{
    /// <summary>The named <see cref="HttpClient"/> registered for CoinGecko requests.</summary>
    public const string HttpClientName = "CoinGecko";

    /// <summary>The service key under which the undecorated <see cref="ICryptocurrencyService"/> is registered.</summary>
    public const string CryptocurrencyServiceKey = "undecorated";

    private const int MaxRetryAttempts = 3;
    private const int DefaultCacheSeconds = 300;

    private static readonly TimeSpan ConstantBackoff = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HttpClientTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Registers <see cref="ICryptocurrencyService"/> and its dependencies.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <c>CoinGecko:ApiKey</c> is missing or blank, or <c>CoinGecko:CacheSeconds</c>
    /// is present and non-blank but not a positive whole number.
    /// </exception>
    public static IServiceCollection AddCryptoStuff(this IServiceCollection services, IConfiguration configuration)
    {
        var apiKey = ReadApiKey(configuration);
        var cacheDuration = ReadCacheDuration(configuration);
        var options = new CoinGeckoClientOptions { ApiKey = apiKey };

        services.AddHttpClient<ICoinGeckoClient, CoinGeckoClient>(HttpClientName, httpClient =>
            {
                options.ApplyTo(httpClient);
                httpClient.Timeout = HttpClientTimeout;
            })
            .AddHttpMessageHandler(() => new RateLimitRetryHandler(MaxRetryAttempts, ConstantBackoff))
            // The keyed CryptocurrencyService below is registered as a singleton,
            // so it resolves and holds this typed client once for the app's whole
            // lifetime instead of getting a fresh one every couple of minutes the
            // way IHttpClientFactory normally rotates handlers. Setting the pooled
            // connection lifetime here keeps the underlying connections (and DNS
            // resolution) refreshing on the same schedule the factory would have
            // used, even though the outer HttpClient instance itself never changes.
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            });

        // CryptocurrencyService is registered as a singleton, keyed so
        // CachingCryptocurrencyService below can resolve it by key. Both must be
        // singletons for the cache to be useful across calls — a scoped/transient
        // CachingCryptocurrencyService would create a fresh, empty cache on every
        // resolution. CachingCryptocurrencyService's constructor (already reviewed
        // in milestone 3.4) takes the inner service directly rather than a
        // factory, so there's no seam to resolve a fresh CryptocurrencyService per
        // call without changing that type.
        services.AddKeyedSingleton<ICryptocurrencyService, CryptocurrencyService>(CryptocurrencyServiceKey);

        services.AddSingleton<ICryptocurrencyService>(sp =>
            new CachingCryptocurrencyService(
                sp.GetRequiredKeyedService<ICryptocurrencyService>(CryptocurrencyServiceKey),
                cacheDuration));

        return services;
    }

    private static string ReadApiKey(IConfiguration configuration)
    {
        var apiKey = configuration["CoinGecko:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "CoinGecko API key is missing. Set it locally by running "
                + "'dotnet user-secrets set CoinGecko:ApiKey <your-api-key>' from the host "
                + "project directory, or set the CoinGecko__ApiKey environment variable in "
                + "deployment.");
        }

        return apiKey;
    }

    private static TimeSpan ReadCacheDuration(IConfiguration configuration)
    {
        var cacheSeconds = configuration["CoinGecko:CacheSeconds"];
        if (string.IsNullOrWhiteSpace(cacheSeconds))
        {
            return TimeSpan.FromSeconds(DefaultCacheSeconds);
        }

        if (!int.TryParse(cacheSeconds, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            throw new InvalidOperationException(
                $"CoinGecko:CacheSeconds is set to '{cacheSeconds}', which is not a positive whole number.");
        }

        return TimeSpan.FromSeconds(parsed);
    }
}
