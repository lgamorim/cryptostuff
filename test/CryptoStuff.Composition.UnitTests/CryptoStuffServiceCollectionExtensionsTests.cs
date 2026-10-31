using System.Reflection;
using CryptoStuff.CoinGecko;
using CryptoStuff.Core;
using CryptoStuff.Core.Watchlists;
using CryptoStuff.Persistence;
using CryptoStuff.Persistence.Watchlists;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoStuff.Composition.UnitTests;

public class CryptoStuffServiceCollectionExtensionsTests
{
    private const string ValidApiKey = "test-api-key";

    // Registration and resolution never open the connection, so the file named
    // here is never created.
    private const string ConfiguredConnectionString = "Data Source=registration-test.db";

    private static IConfiguration BuildConfiguration(Dictionary<string, string?>? values = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(values ?? []).Build();

    [Fact]
    public void Should_ThrowInvalidOperationException_When_ApiKeyIsAbsent()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration();

        var act = () => services.AddCryptoStuff(configuration);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Should_ThrowInvalidOperationException_When_ApiKeyIsWhitespace()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new() { ["CoinGecko:ApiKey"] = "   " });

        var act = () => services.AddCryptoStuff(configuration);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Should_ThrowExactMessage_When_ApiKeyIsMissing()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration();

        var act = () => services.AddCryptoStuff(configuration);

        act.Should().Throw<InvalidOperationException>().WithMessage(
            "CoinGecko API key is missing. Set it locally by running "
            + "'dotnet user-secrets set CoinGecko:ApiKey <your-api-key>' from the host "
            + "project directory, or set the CoinGecko__ApiKey environment variable in "
            + "deployment.");
    }

    [Fact]
    public void Should_ThrowBeforeBuildingServiceProvider_When_ApiKeyIsMissing()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration();

        var act = () => services.AddCryptoStuff(configuration);

        act.Should().Throw<InvalidOperationException>();
        services.Should().BeEmpty();
    }

    [Fact]
    public void Should_ThrowInvalidOperationException_When_CacheSecondsIsNotANumber()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new()
        {
            ["CoinGecko:ApiKey"] = ValidApiKey,
            ["CoinGecko:CacheSeconds"] = "not-a-number",
        });

        var act = () => services.AddCryptoStuff(configuration);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Should_ThrowBeforeBuildingServiceProvider_When_CacheSecondsIsNotANumber()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new()
        {
            ["CoinGecko:ApiKey"] = ValidApiKey,
            ["CoinGecko:CacheSeconds"] = "not-a-number",
        });

        var act = () => services.AddCryptoStuff(configuration);

        act.Should().Throw<InvalidOperationException>();
        services.Should().BeEmpty();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-60")]
    public void Should_ThrowInvalidOperationException_When_CacheSecondsIsNotPositive(string cacheSeconds)
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new()
        {
            ["CoinGecko:ApiKey"] = ValidApiKey,
            ["CoinGecko:CacheSeconds"] = cacheSeconds,
        });

        var act = () => services.AddCryptoStuff(configuration);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Should_DefaultCacheDurationToFiveMinutes_When_CacheSecondsIsWhitespace()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new()
        {
            ["CoinGecko:ApiKey"] = ValidApiKey,
            ["CoinGecko:CacheSeconds"] = "   ",
        });
        services.AddCryptoStuff(configuration);
        using var provider = services.BuildServiceProvider();

        var service = provider.GetRequiredService<ICryptocurrencyService>();

        GetCacheDuration(service).Should().Be(TimeSpan.FromSeconds(300));
    }

    [Fact]
    public void Should_DefaultCacheDurationToFiveMinutes_When_CacheSecondsIsAbsent()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new() { ["CoinGecko:ApiKey"] = ValidApiKey });
        services.AddCryptoStuff(configuration);
        using var provider = services.BuildServiceProvider();

        var service = provider.GetRequiredService<ICryptocurrencyService>();

        GetCacheDuration(service).Should().Be(TimeSpan.FromSeconds(300));
    }

    [Fact]
    public void Should_UseConfiguredCacheDuration_When_CacheSecondsIsPresent()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new()
        {
            ["CoinGecko:ApiKey"] = ValidApiKey,
            ["CoinGecko:CacheSeconds"] = "120",
        });
        services.AddCryptoStuff(configuration);
        using var provider = services.BuildServiceProvider();

        var service = provider.GetRequiredService<ICryptocurrencyService>();

        GetCacheDuration(service).Should().Be(TimeSpan.FromSeconds(120));
    }

    private static TimeSpan GetCacheDuration(ICryptocurrencyService service)
    {
        var field = typeof(CachingCryptocurrencyService).GetField(
            "_cacheDuration", BindingFlags.NonPublic | BindingFlags.Instance);
        return (TimeSpan)field!.GetValue(service)!;
    }

    [Fact]
    public void Should_ReturnSameServiceCollection_When_Called()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new() { ["CoinGecko:ApiKey"] = ValidApiKey });

        var result = services.AddCryptoStuff(configuration);

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void Should_ResolveICryptocurrencyServiceAsCachingCryptocurrencyService_When_Resolved()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new() { ["CoinGecko:ApiKey"] = ValidApiKey });
        services.AddCryptoStuff(configuration);
        using var provider = services.BuildServiceProvider();

        var service = provider.GetRequiredService<ICryptocurrencyService>();

        service.Should().BeOfType<CachingCryptocurrencyService>();
    }

    [Fact]
    public void Should_ResolveSameInstance_When_ICryptocurrencyServiceResolvedTwice()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new() { ["CoinGecko:ApiKey"] = ValidApiKey });
        services.AddCryptoStuff(configuration);
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<ICryptocurrencyService>();
        var second = provider.GetRequiredService<ICryptocurrencyService>();

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void Should_RegisterUndecoratedServiceUnderServiceKey_When_ResolvedByKey()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new() { ["CoinGecko:ApiKey"] = ValidApiKey });
        services.AddCryptoStuff(configuration);
        using var provider = services.BuildServiceProvider();

        var undecorated = provider.GetRequiredKeyedService<ICryptocurrencyService>(
            CryptoStuffServiceCollectionExtensions.CryptocurrencyServiceKey);

        undecorated.Should().BeOfType<CryptocurrencyService>();
    }

    [Fact]
    public void Should_ResolveICoinGeckoClientAsCoinGeckoClient_When_Resolved()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new() { ["CoinGecko:ApiKey"] = ValidApiKey });
        services.AddCryptoStuff(configuration);
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<ICoinGeckoClient>();

        client.Should().BeOfType<CoinGeckoClient>();
    }

    [Fact]
    public void Should_SetBaseAddress_When_HttpClientCreated()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new() { ["CoinGecko:ApiKey"] = ValidApiKey });
        services.AddCryptoStuff(configuration);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var httpClient = factory.CreateClient(CryptoStuffServiceCollectionExtensions.HttpClientName);

        httpClient.BaseAddress.Should().Be(new Uri(CoinGeckoClientOptions.DefaultBaseAddress));
    }

    [Fact]
    public void Should_SetApiKeyHeader_When_HttpClientCreated()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new() { ["CoinGecko:ApiKey"] = ValidApiKey });
        services.AddCryptoStuff(configuration);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var httpClient = factory.CreateClient(CryptoStuffServiceCollectionExtensions.HttpClientName);

        httpClient.DefaultRequestHeaders.GetValues(CoinGeckoClientOptions.ApiKeyHeaderName)
            .Should().ContainSingle().Which.Should().Be(ValidApiKey);
    }

    [Fact]
    public void Should_SetThirtySecondTimeout_When_HttpClientCreated()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new() { ["CoinGecko:ApiKey"] = ValidApiKey });
        services.AddCryptoStuff(configuration);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var httpClient = factory.CreateClient(CryptoStuffServiceCollectionExtensions.HttpClientName);

        httpClient.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Should_IncludeRateLimitRetryHandlerInPipeline_When_HttpMessageHandlerCreated()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new() { ["CoinGecko:ApiKey"] = ValidApiKey });
        services.AddCryptoStuff(configuration);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpMessageHandlerFactory>();

        var handler = factory.CreateHandler(CryptoStuffServiceCollectionExtensions.HttpClientName);

        ContainsRateLimitRetryHandler(handler).Should().BeTrue();
    }

    [Fact]
    public void Should_ThrowBeforeRegisteringAnything_When_ConnectionStringIsInvalid()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new()
        {
            ["CoinGecko:ApiKey"] = ValidApiKey,
            ["ConnectionStrings:CryptoStuff"] = "Server=localhost",
        });

        var act = () => services.AddCryptoStuff(configuration);

        act.Should().Throw<InvalidOperationException>();
        services.Should().BeEmpty();
    }

    [Theory]
    [InlineData(typeof(CryptoStuffDbContext))]
    [InlineData(typeof(IWatchlistRepository))]
    [InlineData(typeof(IWatchlistService))]
    public void Should_RegisterPersistenceServicesAsScoped_When_Called(Type serviceType)
    {
        var services = new ServiceCollection();

        services.AddCryptoStuff(BuildPersistenceConfiguration());

        services.Should().ContainSingle(descriptor => descriptor.ServiceType == serviceType)
            .Which.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Should_RegisterICryptocurrencyServiceAsSingleton_When_Called()
    {
        var services = new ServiceCollection();

        services.AddCryptoStuff(BuildPersistenceConfiguration());

        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(ICryptocurrencyService) && !descriptor.IsKeyedService)
            .Which.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void Should_BuildProvider_When_ScopesAndRegistrationsAreValidated()
    {
        var services = new ServiceCollection();
        services.AddCryptoStuff(BuildPersistenceConfiguration());

        var act = () => services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        act.Should().NotThrow().Which.Dispose();
    }

    [Fact]
    public async Task Should_ResolveWatchlistServiceOverSqliteRepository_When_ResolvedFromScope()
    {
        var services = new ServiceCollection();
        services.AddCryptoStuff(BuildPersistenceConfiguration());
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();

        var service = scope.ServiceProvider.GetRequiredService<IWatchlistService>();
        var repository = scope.ServiceProvider.GetRequiredService<IWatchlistRepository>();

        service.Should().BeOfType<WatchlistService>();
        repository.Should().BeOfType<WatchlistRepository>();
    }

    [Fact]
    public async Task Should_ConfigureContextWithResolvedConnectionString_When_ResolvedFromScope()
    {
        var services = new ServiceCollection();
        services.AddCryptoStuff(BuildPersistenceConfiguration());
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<CryptoStuffDbContext>();

        context.Database.GetConnectionString().Should().Be(ConfiguredConnectionString);
    }

    [Fact]
    public async Task Should_ResolveDistinctContexts_When_ResolvedFromDifferentScopes()
    {
        var services = new ServiceCollection();
        services.AddCryptoStuff(BuildPersistenceConfiguration());
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();

        var first = firstScope.ServiceProvider.GetRequiredService<CryptoStuffDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<CryptoStuffDbContext>();

        second.Should().NotBeSameAs(first);
    }

    private static IConfiguration BuildPersistenceConfiguration() => BuildConfiguration(new()
    {
        ["CoinGecko:ApiKey"] = ValidApiKey,
        ["ConnectionStrings:CryptoStuff"] = ConfiguredConnectionString,
    });

    private static bool ContainsRateLimitRetryHandler(HttpMessageHandler handler)
    {
        var current = handler;
        while (current is DelegatingHandler delegatingHandler)
        {
            if (delegatingHandler is RateLimitRetryHandler)
            {
                return true;
            }

            current = delegatingHandler.InnerHandler;
        }

        return false;
    }
}
