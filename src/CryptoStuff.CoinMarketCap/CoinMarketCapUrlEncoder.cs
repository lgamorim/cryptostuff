namespace CryptoStuff.CoinMarketCap;

/// <summary>
/// Builds comma-separated, percent-encoded lists for CoinMarketCap's
/// list-valued query parameters (e.g. <c>slug</c>, <c>convert</c>).
/// </summary>
internal static class CoinMarketCapUrlEncoder
{
    /// <summary>
    /// Percent-encodes each value and joins the results with a literal comma.
    /// Encoding happens before joining so a value containing a comma or space
    /// cannot be mistaken for an additional list item.
    /// </summary>
    public static string JoinAndEncode(IEnumerable<string> values) =>
        string.Join(',', values.Select(Uri.EscapeDataString));
}
