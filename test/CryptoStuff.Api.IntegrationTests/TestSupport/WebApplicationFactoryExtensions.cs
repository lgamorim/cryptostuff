using Microsoft.AspNetCore.Mvc.Testing;

namespace CryptoStuff.Api.IntegrationTests.TestSupport;

internal static class WebApplicationFactoryExtensions
{
    /// <summary>
    /// A client that sends no <c>X-Api-Key</c> header, for tests about the
    /// callers the API rejects or the routes it leaves open.
    /// </summary>
    public static HttpClient CreateClientWithoutApiKey(this WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Remove(CryptoStuffApiFactory.ApiKeyHeader);
        return client;
    }
}
