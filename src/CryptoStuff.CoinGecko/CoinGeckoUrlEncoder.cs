namespace CryptoStuff.CoinGecko;

/// <summary>
/// Builds comma-separated, percent-encoded identifier lists for CoinGecko's
/// list-valued query parameters (e.g. <c>ids</c>, <c>vs_currencies</c>).
/// </summary>
public static class CoinGeckoUrlEncoder
{
    /// <summary>
    /// Percent-encodes each value and joins the results with a literal comma.
    /// Encoding happens before joining so a value containing a comma or space
    /// cannot be mistaken for an additional list item.
    /// </summary>
    public static string JoinAndEncode(IEnumerable<string> values) =>
        string.Join(',', values.Select(Uri.EscapeDataString));
}
