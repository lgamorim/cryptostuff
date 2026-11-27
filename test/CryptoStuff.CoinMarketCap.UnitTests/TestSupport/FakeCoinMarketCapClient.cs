namespace CryptoStuff.CoinMarketCap.UnitTests.TestSupport;

/// <summary>
/// A stand-in <see cref="ICoinMarketCapClient"/> that answers each request
/// with whatever <c>respond</c> returns for its URI, and records every request.
/// A response is boxed as <see cref="object"/> and cast to the response type
/// the caller asks for, so a script that answers a call with the wrong type
/// fails the test with an <see cref="InvalidCastException"/>.
/// </summary>
internal sealed class FakeCoinMarketCapClient(Func<string, object> respond) : ICoinMarketCapClient
{
    public List<string> RequestUris { get; } = [];

    public List<CancellationToken> CancellationTokens { get; } = [];

    public Task<CoinMarketCapResponse<TValue>> GetAsync<TValue>(string requestUri, CancellationToken cancellationToken)
    {
        RequestUris.Add(requestUri);
        CancellationTokens.Add(cancellationToken);
        return Task.FromResult((CoinMarketCapResponse<TValue>)respond(requestUri));
    }
}
