namespace CryptoStuff.CoinGecko;

/// <summary>
/// Configures the base address and API key used to authenticate against
/// CoinGecko.
/// </summary>
public sealed record CoinGeckoClientOptions
{
    /// <summary>
    /// The public CoinGecko REST API used by a demo API key. Trailing-slashed
    /// so relative request URIs resolve under the `/v3` segment rather than
    /// replacing it, per <see cref="Uri"/> relative-resolution rules.
    /// </summary>
    public const string DefaultBaseAddress = "https://api.coingecko.com/api/v3/";

    /// <summary>The HTTP header CoinGecko expects the demo API key on.</summary>
    public const string ApiKeyHeaderName = "x-cg-demo-api-key";

    /// <summary>The CoinGecko demo API key.</summary>
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
