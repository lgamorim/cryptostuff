using System.Net;
using System.Text;
using CryptoStuff.Composition.UnitTests.TestSupport;
using CryptoStuff.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoStuff.Composition.UnitTests;

/// <summary>
/// Checks that <see cref="CryptoStuffServiceCollectionExtensions.AddCryptoStuff"/>
/// puts <see cref="NegativePriceGuard"/> in the registered service's chain:
/// the CoinGecko client's primary handler is swapped for a scripted one that
/// answers with a price below zero, so no request leaves the process, and the
/// resolved <see cref="ICryptocurrencyService"/> must come back without it.
/// </summary>
public sealed class NegativePriceGuardRegistrationTests
{
    [Fact]
    public async Task Should_DropTheNegativeAmount_When_PricesRequestedThroughRegisteredService()
    {
        var services = new ServiceCollection();
        services.AddCryptoStuff(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["CoinGecko:ApiKey"] = "test-api-key" })
            .Build());
        services.AddHttpClient(CryptoStuffServiceCollectionExtensions.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => SequencedHttpMessageHandler.ReturningInOrder(
                () => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"bitcoin":{"usd":43000,"eur":-1}}""", Encoding.UTF8, "application/json"),
                }));
        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<ICryptocurrencyService>();

        var result = await service.GetPricesAsync(
            new CoinPriceQuery { CoinIds = ["bitcoin"], VsCurrencies = ["usd", "eur"] }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // `!` is safe: a successful result always carries its value.
        result.Value!.Coins.Should().BeEquivalentTo(
            [new CoinPrice { CoinId = "bitcoin", Amounts = [new CurrencyAmount { Currency = "usd", Amount = 43000m }] }]);
    }
}
