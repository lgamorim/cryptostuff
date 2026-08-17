using CryptoStuff.CoinGecko;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CryptoStuff.Api.IntegrationTests.TestSupport;

/// <summary>
/// Hosts <c>CryptoStuff.Api</c> for real, with only <see cref="ICoinGeckoClient"/>
/// substituted by a <see cref="FakeCoinGeckoClient"/> — every other registration
/// from <c>AddCryptoStuff</c> (the service, the caching decorator, validation)
/// and the API host's own routing runs unmodified.
/// </summary>
internal sealed class CryptoStuffApiFactory : WebApplicationFactory<Program>
{
    /// <summary>The fake substituted for the real CoinGecko client. Configure its response before issuing a request.</summary>
    public FakeCoinGeckoClient CoinGeckoClient { get; } = new();

    public CryptoStuffApiFactory()
    {
        // AddCryptoStuff reads CoinGecko:ApiKey synchronously in Program.cs,
        // before the host builds - i.e. before WebApplicationFactory's own
        // configuration-override hooks are guaranteed to have applied. A real
        // process environment variable, which AddEnvironmentVariables() picks
        // up unconditionally, sidesteps that timing question entirely, and
        // doubles as coverage that the documented CoinGecko__ApiKey deployment
        // mechanism (see README) actually works.
        Environment.SetEnvironmentVariable("CoinGecko__ApiKey", "integration-test-key");
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICoinGeckoClient>();
            services.AddSingleton<ICoinGeckoClient>(CoinGeckoClient);
        });
    }
}
