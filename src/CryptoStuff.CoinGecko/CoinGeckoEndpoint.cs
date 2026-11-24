namespace CryptoStuff.CoinGecko;

/// <summary>
/// Names the CoinGecko endpoint a request URI calls, as the fixed template
/// <see cref="CoinGeckoClientExtensions"/> builds it from — never the URI
/// itself, which carries coin ids, currencies, dates and a query string. The
/// <c>endpoint</c> tag on the request counter takes its value from here, so
/// its values stay a small fixed set.
/// </summary>
internal static class CoinGeckoEndpoint
{
    /// <summary>The value for any request URI that matches none of the templates.</summary>
    public const string Other = "other";

    /// <summary>
    /// Returns the template <paramref name="requestUri"/>'s path matches, or
    /// <see cref="Other"/>. Every path parameter the extensions insert is
    /// escaped with <see cref="Uri.EscapeDataString(string)"/>, so a <c>/</c> in the
    /// path always separates segments.
    /// </summary>
    public static string TemplateOf(string requestUri)
    {
        var queryStart = requestUri.IndexOf('?', StringComparison.Ordinal);
        var path = queryStart < 0 ? requestUri : requestUri[..queryStart];
        return path.Split('/') switch
        {
            ["simple", "price"] => "simple/price",
            ["simple", "token_price", { Length: > 0 }] => "simple/token_price/{platform}",
            ["coins", { Length: > 0 }] => "coins/{id}",
            ["coins", { Length: > 0 }, "market_chart"] => "coins/{id}/market_chart",
            ["coins", { Length: > 0 }, "history"] => "coins/{id}/history",
            _ => Other,
        };
    }
}
