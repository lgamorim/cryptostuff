using CryptoStuff.CoinGecko;

namespace CryptoStuff.Core.UnitTests.TestSupport;

internal sealed class FakeCoinGeckoClient(object response) : ICoinGeckoClient
{
    public string? CapturedRequestUri { get; private set; }

    public CancellationToken CapturedCancellationToken { get; private set; }

    public Task<CoinGeckoResponse<TValue>> GetAsync<TValue>(string requestUri, CancellationToken cancellationToken)
    {
        CapturedRequestUri = requestUri;
        CapturedCancellationToken = cancellationToken;
        return Task.FromResult((CoinGeckoResponse<TValue>)response);
    }
}
