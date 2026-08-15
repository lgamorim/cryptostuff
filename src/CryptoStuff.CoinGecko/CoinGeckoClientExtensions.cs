using CoinGeckoPriceMatrix = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, decimal>>;

namespace CryptoStuff.CoinGecko;

/// <summary>
/// Endpoint-specific calls composed from <see cref="ICoinGeckoClient.GetAsync{TValue}"/>,
/// per that interface's documented extension point.
/// </summary>
public static class CoinGeckoClientExtensions
{
    /// <summary>
    /// Calls <c>simple/price</c> for the given coin ids and target currencies.
    /// The result's outer key is the coin id, inner key a lowercase
    /// vs_currency code (e.g. "usd"), value the price.
    /// </summary>
    public static Task<CoinGeckoResponse<CoinGeckoPriceMatrix>> GetSimplePriceAsync(
        this ICoinGeckoClient client,
        IEnumerable<string> ids,
        IEnumerable<string> vsCurrencies,
        CancellationToken cancellationToken) =>
        client.GetAsync<CoinGeckoPriceMatrix>(
            $"simple/price?ids={CoinGeckoUrlEncoder.JoinAndEncode(ids)}&vs_currencies={CoinGeckoUrlEncoder.JoinAndEncode(vsCurrencies)}",
            cancellationToken);

    /// <summary>
    /// Calls <c>simple/token_price/{platform}</c> for the given contract
    /// addresses and target currencies. The result's outer key is the
    /// contract address, inner key a lowercase vs_currency code (e.g. "usd"),
    /// value the price.
    /// </summary>
    public static Task<CoinGeckoResponse<CoinGeckoPriceMatrix>> GetSimpleTokenPriceAsync(
        this ICoinGeckoClient client,
        string platform,
        IEnumerable<string> contractAddresses,
        IEnumerable<string> vsCurrencies,
        CancellationToken cancellationToken) =>
        client.GetAsync<CoinGeckoPriceMatrix>(
            $"simple/token_price/{Uri.EscapeDataString(platform)}?contract_addresses={CoinGeckoUrlEncoder.JoinAndEncode(contractAddresses)}&vs_currencies={CoinGeckoUrlEncoder.JoinAndEncode(vsCurrencies)}",
            cancellationToken);

    /// <summary>
    /// Calls <c>coins/{id}/market_chart</c> for the given coin id, target
    /// currency, and day count, returning the prices, market caps, and total
    /// volumes series.
    /// </summary>
    public static Task<CoinGeckoResponse<CoinGeckoMarketChart>> GetMarketChartAsync(
        this ICoinGeckoClient client,
        string coinId,
        string vsCurrency,
        int days,
        CancellationToken cancellationToken) =>
        client.GetAsync<CoinGeckoMarketChart>(
            $"coins/{Uri.EscapeDataString(coinId)}/market_chart?vs_currency={Uri.EscapeDataString(vsCurrency)}&days={days}",
            cancellationToken);

    /// <summary>
    /// Calls <c>coins/{id}</c> for coin detail. <c>description</c> (what
    /// <see cref="CoinGeckoCoin.Description"/> maps) is returned regardless
    /// of the <c>localization</c> flag, so it's turned off here — it only
    /// gates a separate, unmodeled block of translated coin names. The other
    /// flags keep unused large sub-objects (tickers, community data,
    /// developer data, sparkline) out of the response.
    /// </summary>
    public static Task<CoinGeckoResponse<CoinGeckoCoin>> GetCoinAsync(
        this ICoinGeckoClient client,
        string coinId,
        CancellationToken cancellationToken) =>
        client.GetAsync<CoinGeckoCoin>(
            $"coins/{Uri.EscapeDataString(coinId)}?localization=false&tickers=false&market_data=true&community_data=false&developer_data=false&sparkline=false",
            cancellationToken);

    /// <summary>
    /// Calls <c>coins/{id}/history</c> for point-in-time developer activity.
    /// <paramref name="date"/> is passed through verbatim, unvalidated —
    /// parsing and validating CoinGecko's required <c>dd-MM-yyyy</c> format
    /// is Core's job, not this client's.
    /// </summary>
    public static Task<CoinGeckoResponse<CoinGeckoCoinHistory>> GetCoinHistoryAsync(
        this ICoinGeckoClient client,
        string coinId,
        string date,
        CancellationToken cancellationToken) =>
        client.GetAsync<CoinGeckoCoinHistory>(
            $"coins/{Uri.EscapeDataString(coinId)}/history?date={Uri.EscapeDataString(date)}&localization=false",
            cancellationToken);
}
