using System.Globalization;
using CryptoStuff.Core;

namespace CryptoStuff.CoinMarketCap;

/// <summary>
/// The <see cref="ICryptocurrencyService"/> backed by an
/// <see cref="ICoinMarketCapClient"/>, translating the app's CoinGecko ids
/// through <see cref="CoinMarketCapIds"/>. The <see cref="TimeProvider"/>
/// supplies the instant a market chart's history runs up to.
/// </summary>
public sealed class CoinMarketCapCryptocurrencyService(
    ICoinMarketCapClient client,
    TimeProvider timeProvider,
    CoinMarketCapServiceOptions options) : ICryptocurrencyService
{
    /// <summary>The most days of history CoinMarketCap's Basic plan serves.</summary>
    private const int BasicPlanHistoryDays = 365;

    /// <summary>The currency a coin's numeric id is looked up in; any would do, as only the id is kept.</summary>
    private const string IdLookupCurrency = "USD";

    /// <inheritdoc />
    /// <remarks>
    /// One <c>quotes/latest</c> call per currency, with every coin in each, as
    /// <see cref="CoinMarketCapLatestQuoteReader"/> describes. An id that is
    /// another coin's override slug is no coin CoinMarketCap is asked for, so
    /// it fails the result as a missing coin does, with no call made. A price
    /// below zero is passed on as it is.
    /// </remarks>
    public async Task<ServiceResult<CoinPriceView>> GetPricesAsync(CoinPriceQuery query, CancellationToken cancellationToken)
    {
        if (SlugsByRequestedId(query.CoinIds) is not { } slugById)
        {
            return ServiceResult<CoinPriceView>.Failure(ServiceErrorCode.NotFound);
        }

        // No two ids share a slug, so each slug is asked for once.
        var read = await CoinMarketCapLatestQuoteReader.ReadAsync(client, slugById.Values, query.VsCurrencies, cancellationToken);
        if (read.ErrorCode is { } errorCode)
        {
            return ServiceResult<CoinPriceView>.Failure(errorCode, read.ErrorDetail);
        }

        // `!` is safe: a result with no error code succeeded, and a successful
        // result always carries its value.
        var coins = read.Value!;
        return ServiceResult<CoinPriceView>.Success(new CoinPriceView
        {
            Coins = query.CoinIds
                .Select(coinId => new CoinPrice { CoinId = coinId, Amounts = AmountsOf(coins[slugById[coinId]], query.VsCurrencies) })
                .ToArray(),
        });
    }

    /// <inheritdoc />
    /// <remarks>
    /// A query naming no addresses succeeds with none and makes no call. The
    /// platform must be one <see cref="CoinMarketCapIds.PlatformSlugs"/> lists,
    /// otherwise the result is <see cref="ServiceErrorCode.NotFound"/> with no
    /// call made. Each address resolves through
    /// <see cref="CoinMarketCapContractResolver"/>, and the coins they resolve
    /// to are priced as <see cref="GetPricesAsync"/> prices coins, keyed by the
    /// address as requested.
    /// </remarks>
    public async Task<ServiceResult<TokenPriceView>> GetTokenPricesAsync(TokenPriceQuery query, CancellationToken cancellationToken)
    {
        if (query.ContractAddresses.Count == 0)
        {
            return ServiceResult<TokenPriceView>.Success(new TokenPriceView { Tokens = [] });
        }

        var platform = CoinMarketCapIds.PlatformSlugOf(query.Platform);
        if (platform.ErrorCode is { } platformError)
        {
            return ServiceResult<TokenPriceView>.Failure(platformError, platform.ErrorDetail);
        }

        // `!` is safe: a result with no error code succeeded, and a successful
        // result always carries its value.
        var resolved = await CoinMarketCapContractResolver.ResolveAsync(
            client, query.ContractAddresses.Distinct(StringComparer.Ordinal), platform.Value!, cancellationToken);
        if (resolved.ErrorCode is { } resolveError)
        {
            return ServiceResult<TokenPriceView>.Failure(resolveError, resolved.ErrorDetail);
        }

        // `!` is safe, as above.
        var slugByAddress = resolved.Value!;
        var read = await CoinMarketCapLatestQuoteReader.ReadAsync(
            client, slugByAddress.Values.Distinct(StringComparer.Ordinal).ToArray(), query.VsCurrencies, cancellationToken);
        if (read.ErrorCode is { } readError)
        {
            return ServiceResult<TokenPriceView>.Failure(readError, read.ErrorDetail);
        }

        // `!` is safe, as above.
        var coins = read.Value!;
        return ServiceResult<TokenPriceView>.Success(new TokenPriceView
        {
            Tokens = query.ContractAddresses
                .Select(address => new TokenPrice { ContractAddress = address, Amounts = AmountsOf(coins[slugByAddress[address]], query.VsCurrencies) })
                .ToArray(),
        });
    }

    /// <inheritdoc />
    /// <remarks>
    /// CoinMarketCap's Basic plan serves at most 365 days of history, so a
    /// longer query fails with <see cref="ServiceErrorCode.UpstreamUnavailable"/>,
    /// as the plan's own refusal would, and no call is made. <c>quotes/historical</c>
    /// takes only CoinMarketCap's numeric id, so a <c>quotes/latest</c> call in
    /// USD looks it up first, as <see cref="CoinMarketCapLatestQuoteReader"/>
    /// describes. The history then runs back the requested days from now, one
    /// point a day, mapped by <see cref="CoinMarketCapMarketChartMapper"/>. A
    /// history holding no series for the coin is
    /// <see cref="ServiceErrorCode.NotFound"/>, as is an id that is another
    /// coin's override slug, with no call made.
    /// </remarks>
    public async Task<ServiceResult<CoinMarketChartView>> GetMarketChartAsync(CoinMarketChartQuery query, CancellationToken cancellationToken)
    {
        if (query.Days > BasicPlanHistoryDays)
        {
            return ServiceResult<CoinMarketChartView>.Failure(
                ServiceErrorCode.UpstreamUnavailable,
                $"CoinMarketCap's Basic plan serves at most {BasicPlanHistoryDays} days of history.");
        }

        if (CoinMarketCapIds.SlugOf(query.CoinId) is not { } slug)
        {
            return ServiceResult<CoinMarketChartView>.Failure(ServiceErrorCode.NotFound);
        }

        var read = await CoinMarketCapLatestQuoteReader.ReadAsync(client, [slug], [IdLookupCurrency], cancellationToken);
        if (read.ErrorCode is { } readError)
        {
            return ServiceResult<CoinMarketChartView>.Failure(readError, read.ErrorDetail);
        }

        // `!` is safe: a result with no error code succeeded, and a successful
        // result always carries its value.
        var id = read.Value![slug].Id;
        var currency = query.VsCurrency.ToUpperInvariant();
        var end = timeProvider.GetUtcNow();
        var response = await client.GetHistoricalQuotesAsync(id, currency, end.AddDays(-query.Days), end, cancellationToken);
        if (!response.IsSuccess)
        {
            return ServiceResult<CoinMarketChartView>.Failure(CoinMarketCapErrorMapper.Map(response));
        }

        // Upstream JSON can carry a null data map or a null coin.
        if (response.Value?.GetValueOrDefault(id.ToString(CultureInfo.InvariantCulture)) is not { } coin)
        {
            return ServiceResult<CoinMarketChartView>.Failure(ServiceErrorCode.NotFound);
        }

        return ServiceResult<CoinMarketChartView>.Success(CoinMarketCapMarketChartMapper.Map(coin, currency));
    }

    /// <inheritdoc />
    /// <remarks>
    /// The coin is quoted in each of <see cref="CoinMarketCapServiceOptions.DetailCurrencies"/>,
    /// as <see cref="CoinMarketCapLatestQuoteReader"/> describes, and its
    /// metadata is read from <c>info</c>. The market data holds a snapshot for
    /// each of those currencies whose quote has a price, a market cap and a
    /// volume, ordered by currency code, and is null when none has all three.
    /// The symbol is lower-cased, as CoinGecko gives it. A coin missing from
    /// either answer is <see cref="ServiceErrorCode.NotFound"/>, as is an id
    /// that is another coin's override slug, with no call made.
    /// </remarks>
    public async Task<ServiceResult<CoinView>> GetCoinAsync(CoinQuery query, CancellationToken cancellationToken)
    {
        if (CoinMarketCapIds.SlugOf(query.CoinId) is not { } slug)
        {
            return ServiceResult<CoinView>.Failure(ServiceErrorCode.NotFound);
        }

        var read = await CoinMarketCapLatestQuoteReader.ReadAsync(client, [slug], options.DetailCurrencies, cancellationToken);
        if (read.ErrorCode is { } readError)
        {
            return ServiceResult<CoinView>.Failure(readError, read.ErrorDetail);
        }

        var response = await client.GetInfoBySlugAsync([slug], cancellationToken);
        if (!response.IsSuccess)
        {
            return ServiceResult<CoinView>.Failure(CoinMarketCapErrorMapper.Map(response));
        }

        // Upstream JSON can carry a null data map, a null coin or a null slug,
        // and the map's keys may not be slugs, so the coin is matched by slug.
        if ((response.Value?.Values ?? []).FirstOrDefault(coin => string.Equals(coin?.Slug, slug, StringComparison.Ordinal)) is not { } info)
        {
            return ServiceResult<CoinView>.Failure(ServiceErrorCode.NotFound);
        }

        // `!` is safe: a result with no error code succeeded, and a successful
        // result always carries its value.
        var snapshots = SnapshotsOf(read.Value![slug]);
        return ServiceResult<CoinView>.Success(new CoinView
        {
            Id = query.CoinId,
            Symbol = info.Symbol?.ToLowerInvariant() ?? string.Empty,
            Name = info.Name ?? string.Empty,
            Description = info.Description,
            ImageUrl = info.Logo,
            MarketData = snapshots.Length == 0 ? null : snapshots,
        });
    }

    /// <inheritdoc />
    /// <remarks>CoinMarketCap has no developer data, so this fails with no call made.</remarks>
    public Task<ServiceResult<CoinDeveloperDataView>> GetDeveloperDataAsync(CoinDeveloperDataQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(ServiceResult<CoinDeveloperDataView>.Failure(ServiceErrorCode.Unsupported, "CoinMarketCap provides no developer data."));

    /// <summary>
    /// Each distinct requested coin id with the slug it is asked for as, or
    /// null when any id has no slug, being another coin's override slug.
    /// </summary>
    private static Dictionary<string, string>? SlugsByRequestedId(IEnumerable<string> coinIds)
    {
        var requested = coinIds.Distinct(StringComparer.Ordinal).ToArray();
        var slugById = CoinMarketCapIds.RequestedCoinIdsBySlug(requested)
            .SelectMany(group => group, (group, coinId) => (CoinId: coinId, Slug: group.Key))
            .ToDictionary(pair => pair.CoinId, pair => pair.Slug, StringComparer.Ordinal);
        return slugById.Count == requested.Length ? slugById : null;
    }

    /// <summary>
    /// A coin's price in each of <paramref name="currencies"/> that it has one
    /// in, in that order, under the lower-case code, once per code however
    /// many cases the query names it in, as it was asked for once.
    /// </summary>
    private static CurrencyAmount[] AmountsOf(CoinMarketCapLatestCoin coin, IEnumerable<string> currencies) =>
        currencies
            .Select(currency => currency.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Select(currency => (Currency: currency, Price: coin.QuotesByCurrency.GetValueOrDefault(currency)?.Price))
            .Where(amount => amount.Price is not null)
            .Select(amount => new CurrencyAmount { Currency = amount.Currency, Amount = amount.Price.GetValueOrDefault() })
            .ToArray();

    /// <summary>A snapshot of the coin in each currency it has a price, a market cap and a volume in, ordered by currency code.</summary>
    private static CoinCurrencySnapshot[] SnapshotsOf(CoinMarketCapLatestCoin coin) =>
        coin.QuotesByCurrency
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value is { Price: { } price, MarketCap: { } marketCap, Volume24h: { } volume }
                ? new CoinCurrencySnapshot { Currency = pair.Key, Price = price, MarketCap = marketCap, Volume = volume }
                : null)
            .OfType<CoinCurrencySnapshot>()
            .ToArray();
}
