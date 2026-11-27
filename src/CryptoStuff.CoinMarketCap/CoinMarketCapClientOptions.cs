namespace CryptoStuff.CoinMarketCap;

/// <summary>
/// Configures the base address and API key used to authenticate against
/// CoinMarketCap.
/// </summary>
public sealed record CoinMarketCapClientOptions
{
    /// <summary>
    /// CoinMarketCap's professional API, which every plan's key, Basic
    /// included, is sent to. Trailing-slashed so a relative request URI such
    /// as <c>v3/cryptocurrency/quotes/latest</c> resolves under it, per
    /// <see cref="Uri"/> relative-resolution rules.
    /// </summary>
    public const string DefaultBaseAddress = "https://pro-api.coinmarketcap.com/";

    /// <summary>The HTTP header CoinMarketCap expects the API key on.</summary>
    public const string ApiKeyHeaderName = "X-CMC_PRO_API_KEY";

    /// <summary>The CoinMarketCap API key.</summary>
    public required string ApiKey { get; init; }

    /// <summary>The base address requests are sent against. Defaults to <see cref="DefaultBaseAddress"/>.</summary>
    public Uri BaseAddress { get; init; } = new(DefaultBaseAddress);

    /// <summary>Sets <paramref name="httpClient"/>'s base address and API key header.</summary>
    public void ApplyTo(HttpClient httpClient)
    {
        httpClient.BaseAddress = BaseAddress;
        httpClient.DefaultRequestHeaders.Add(ApiKeyHeaderName, ApiKey);
    }
}
