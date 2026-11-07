using System.Globalization;
using CryptoStuff.CoinGecko;
using CryptoStuff.Core;
using CryptoStuff.Core.Alerts;
using CryptoStuff.Core.Portfolios;
using CryptoStuff.Core.Watchlists;
using CryptoStuff.Persistence;
using CryptoStuff.Persistence.Alerts;
using CryptoStuff.Persistence.Portfolios;
using CryptoStuff.Persistence.Watchlists;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CryptoStuff.Composition;

/// <summary>
/// Registers everything <c>CryptoStuff</c> needs to serve cryptocurrency data:
/// the CoinGecko-backed <see cref="ICryptocurrencyService"/>, wrapped in its
/// caching decorator, wired to a CoinGecko <see cref="HttpClient"/> with the
/// rate-limit retry handler attached, and the scoped SQLite-backed features
/// over <see cref="CryptoStuffDbContext"/>.
/// </summary>
public static class CryptoStuffServiceCollectionExtensions
{
    /// <summary>The named <see cref="HttpClient"/> registered for CoinGecko requests.</summary>
    public const string HttpClientName = "CoinGecko";

    /// <summary>The service key under which the undecorated <see cref="ICryptocurrencyService"/> is registered.</summary>
    public const string CryptocurrencyServiceKey = "undecorated";

    private const int MaxRetryAttempts = 3;
    private const int DefaultCacheSeconds = 300;
    private const int DefaultEvaluationSeconds = 60;

    // The longest whole-second period a PeriodicTimer accepts: its limit is
    // 4294967294 ms, and a longer one would only fail once the host started.
    private const int MaxEvaluationSeconds = 4294967;

    private static readonly TimeSpan ConstantBackoff = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HttpClientTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Registers <see cref="ICryptocurrencyService"/> and its dependencies as
    /// singletons, <see cref="TimeProvider.System"/> as the singleton
    /// <see cref="TimeProvider"/> unless one is already registered, and
    /// <see cref="CryptoStuffDbContext"/>,
    /// <see cref="IWatchlistRepository"/>, <see cref="IWatchlistService"/>,
    /// <see cref="IPortfolioRepository"/>, <see cref="IPortfolioService"/>,
    /// <see cref="IAlertRepository"/>, <see cref="IAlertService"/>, and
    /// <see cref="IAlertEvaluator"/> as scoped. The database is located by
    /// <see cref="DatabaseConnectionString.Resolve"/>; nothing here opens it.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <c>CoinGecko:ApiKey</c> is missing or blank, <c>CoinGecko:CacheSeconds</c>
    /// is present and non-blank but not a positive whole number, or the database
    /// connection string cannot be resolved.
    /// </exception>
    public static IServiceCollection AddCryptoStuff(this IServiceCollection services, IConfiguration configuration)
    {
        var apiKey = ReadApiKey(configuration);
        var cacheDuration = ReadCacheDuration(configuration);
        var connectionString = DatabaseConnectionString.Resolve(
            configuration,
            // DoNotVerify: the folder may not exist yet on a fresh machine, and
            // migrating creates the database's directory anyway.
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.DoNotVerify));
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

        // The clock the portfolio rules read. TryAdd leaves one a host or a
        // test registered first in place.
        services.TryAddSingleton(TimeProvider.System);

        services.AddDbContext<CryptoStuffDbContext>(dbContextOptions => dbContextOptions.UseSqlite(connectionString));
        services.AddScoped<IWatchlistRepository, WatchlistRepository>();
        services.AddScoped<IWatchlistService, WatchlistService>();
        services.AddScoped<IPortfolioRepository, PortfolioRepository>();
        services.AddScoped<IPortfolioService, PortfolioService>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IAlertEvaluator, AlertEvaluator>();

        return services;
    }

    /// <summary>
    /// Registers the singleton <see cref="AlertEvaluationSchedule"/> the API
    /// host's background alert evaluation runs on: every
    /// <c>Alerts:EvaluationSeconds</c> seconds, or every 60 when that is absent
    /// or blank. Only the API host evaluates in the background, so only it
    /// calls this; the CLI never reads the setting.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <c>Alerts:EvaluationSeconds</c> is present and non-blank but not a whole
    /// number from 1 to 4294967, the longest period a <see cref="PeriodicTimer"/> accepts.
    /// </exception>
    public static IServiceCollection AddAlertEvaluationSchedule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(new AlertEvaluationSchedule { Interval = ReadEvaluationInterval(configuration) });
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

    private static TimeSpan ReadEvaluationInterval(IConfiguration configuration)
    {
        var evaluationSeconds = configuration["Alerts:EvaluationSeconds"];
        if (string.IsNullOrWhiteSpace(evaluationSeconds))
        {
            return TimeSpan.FromSeconds(DefaultEvaluationSeconds);
        }

        if (!int.TryParse(evaluationSeconds, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed is <= 0 or > MaxEvaluationSeconds)
        {
            throw new InvalidOperationException(
                $"Alerts:EvaluationSeconds is set to '{evaluationSeconds}', which is not a whole number of seconds from 1 to {MaxEvaluationSeconds}. "
                + $"Set it to the number of seconds between background alert evaluations, or remove it to evaluate every {DefaultEvaluationSeconds} seconds.");
        }

        return TimeSpan.FromSeconds(parsed);
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
