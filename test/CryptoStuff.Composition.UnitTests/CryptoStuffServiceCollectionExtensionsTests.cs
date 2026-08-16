using System.Reflection;
using CryptoStuff.CoinGecko;
using CryptoStuff.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoStuff.Composition.UnitTests;

public class CryptoStuffServiceCollectionExtensionsTests
{
    private const string ValidApiKey = "test-api-key";

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
