using CryptoStuff.Core;

namespace CryptoStuff.CoinMarketCap;

/// <summary>
/// Reads coins' latest quotes from <c>quotes/latest</c> the way CoinMarketCap's
/// Basic plan allows, one call per currency with every coin in each, and
/// settles which coins exist.
/// </summary>
internal static class CoinMarketCapLatestQuoteReader
{
    private const string SettlingCurrency = "USD";

    /// <summary>
    /// Reads every slug in <paramref name="slugs"/>, which holds each slug once,
    /// in every currency in <paramref name="currencies"/>, keyed by slug, each
    /// coin's quotes keyed by lower-case currency.
    /// </summary>
    /// <remarks>
    /// Currencies go out upper case, each once whatever its case. A coin
    /// missing from any answer fails the read with
    /// <see cref="ServiceErrorCode.NotFound"/>, and no further call is made. A
    /// currency CoinMarketCap rejects leaves every coin without a quote in it.
    /// When no currency call is answered, because each was rejected or none was
    /// asked for, nothing has said whether the coins exist, so one more call,
    /// in USD, settles it; its quotes are not kept. An answer rejecting the
    /// <c>slug</c> parameter, which <c>skip_invalid=true</c> should prevent but
    /// CoinMarketCap may still send when no slug is valid, fails the read as a
    /// missing coin does. Any other failure fails the read. No slugs make no call. Upstream JSON's explicit nulls, which
    /// <c>required</c> lets through, are skipped: a null coin, or one with a
    /// null slug, counts as missing, and a null quote list, quote or symbol as
    /// no quote.
    /// </remarks>
    public static async Task<ServiceResult<IReadOnlyDictionary<string, CoinMarketCapLatestCoin>>> ReadAsync(
        ICoinMarketCapClient client,
        IReadOnlyCollection<string> slugs,
        IEnumerable<string> currencies,
        CancellationToken cancellationToken)
    {
        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        var quotes = slugs.ToDictionary(slug => slug, _ => new Dictionary<string, CoinMarketCapCurrencyQuote>(StringComparer.Ordinal), StringComparer.Ordinal);
        if (slugs.Count > 0)
        {
            var answered = false;
            foreach (var currency in currencies.Select(currency => currency.ToUpperInvariant()).Distinct(StringComparer.Ordinal))
            {
                var response = await client.GetLatestQuotesAsync(slugs, [currency], cancellationToken);
                if (CoinMarketCapErrorMapper.RejectsParameter(response, "convert"))
                {
                    continue;
                }

                if (Record(response, currency, ids, quotes) is { } failure)
                {
                    return ServiceResult<IReadOnlyDictionary<string, CoinMarketCapLatestCoin>>.Failure(failure);
                }

                answered = true;
            }

            if (!answered)
            {
                var response = await client.GetLatestQuotesAsync(slugs, [SettlingCurrency], cancellationToken);
                if (Record(response, currency: null, ids, quotes) is { } failure)
                {
                    return ServiceResult<IReadOnlyDictionary<string, CoinMarketCapLatestCoin>>.Failure(failure);
                }
            }
        }

        return ServiceResult<IReadOnlyDictionary<string, CoinMarketCapLatestCoin>>.Success(
            slugs.ToDictionary(slug => slug, slug => new CoinMarketCapLatestCoin(ids[slug], quotes[slug]), StringComparer.Ordinal));
    }

    /// <summary>
    /// Records each requested coin's id from <paramref name="response"/>, and
    /// its quote in <paramref name="currency"/> unless that is null, and
    /// returns the error the read fails with, or null.
    /// </summary>
    private static ServiceErrorCode? Record(
        CoinMarketCapResponse<IReadOnlyList<CoinMarketCapLatestQuote>> response,
        string? currency,
        Dictionary<string, int> ids,
        Dictionary<string, Dictionary<string, CoinMarketCapCurrencyQuote>> quotes)
    {
        if (CoinMarketCapErrorMapper.RejectsParameter(response, "slug"))
        {
            return ServiceErrorCode.NotFound;
        }

        if (!response.IsSuccess)
        {
            return CoinMarketCapErrorMapper.Map(response);
        }

        var answer = new Dictionary<string, CoinMarketCapLatestQuote>(StringComparer.Ordinal);
        foreach (var coin in response.Value ?? [])
        {
            if (coin?.Slug is { } slug)
            {
                answer.TryAdd(slug, coin);
            }
        }

        if (quotes.Keys.Any(slug => !answer.ContainsKey(slug)))
        {
            return ServiceErrorCode.NotFound;
        }

        foreach (var (slug, coinQuotes) in quotes)
        {
            var coin = answer[slug];
            ids[slug] = coin.Id;
            if (currency is not null
                && (coin.Quote ?? []).FirstOrDefault(quote => string.Equals(quote?.Symbol, currency, StringComparison.OrdinalIgnoreCase)) is { } quote)
            {
                coinQuotes[currency.ToLowerInvariant()] = quote;
            }
        }

        return null;
    }
}

/// <summary>A coin read by <see cref="CoinMarketCapLatestQuoteReader"/>: its numeric id and its quotes by lower-case currency.</summary>
internal sealed record CoinMarketCapLatestCoin(int Id, IReadOnlyDictionary<string, CoinMarketCapCurrencyQuote> QuotesByCurrency);
