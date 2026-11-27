namespace CryptoStuff.CoinMarketCap;

/// <summary>
/// Names the CoinMarketCap endpoint a request URI calls, as the fixed path
/// <see cref="CoinMarketCapClientExtensions"/> builds it from — never the URI
/// itself, which carries slugs, currencies, dates and contract addresses in
/// its query string. The <c>endpoint</c> tag on the request counter takes its
/// value from here, so its values stay a small fixed set.
/// </summary>
internal static class CoinMarketCapEndpoint
{
    /// <summary>The value for any request URI whose path is none of the known endpoints.</summary>
    public const string Other = "other";

    private static readonly HashSet<string> KnownPaths = new(StringComparer.Ordinal)
    {
        "v3/cryptocurrency/quotes/latest",
        "v2/cryptocurrency/info",
        "v3/cryptocurrency/quotes/historical",
    };

    /// <summary>
    /// Returns <paramref name="requestUri"/>'s path when it is one of the
    /// known endpoints, or <see cref="Other"/>. Every CoinMarketCap endpoint
    /// takes its identifiers in the query string, so the path alone names it.
    /// </summary>
    public static string PathOf(string requestUri)
    {
        var queryStart = requestUri.IndexOf('?', StringComparison.Ordinal);
        var path = queryStart < 0 ? requestUri : requestUri[..queryStart];
        return KnownPaths.Contains(path) ? path : Other;
    }
}
