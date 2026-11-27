using CryptoStuff.Core;

namespace CryptoStuff.CoinMarketCap;

/// <summary>Resolves token contract addresses to the CoinMarketCap coins they belong to.</summary>
internal static class CoinMarketCapContractResolver
{
    /// <summary>
    /// Resolves each address in <paramref name="contractAddresses"/>, which
    /// holds each address once, through <c>info?address=</c>, to the slug of
    /// the coin whose contract list holds that address, in any case, on the
    /// platform whose coin's slug is <paramref name="platformSlug"/>.
    /// </summary>
    /// <remarks>
    /// One call per address, in order. An address CoinMarketCap rejects, or
    /// one whose answer holds no coin listing it on that platform, fails the
    /// resolution with <see cref="ServiceErrorCode.NotFound"/>, and no further
    /// call is made; any other failure fails it too. A platform slug names the
    /// platform's coin, so platforms sharing one, as BNB Smart Chain and opBNB
    /// share <c>bnb</c>, are not told apart. Upstream JSON's explicit nulls,
    /// which <c>required</c> lets through, count as no match.
    /// </remarks>
    public static async Task<ServiceResult<IReadOnlyDictionary<string, string>>> ResolveAsync(
        ICoinMarketCapClient client,
        IEnumerable<string> contractAddresses,
        string platformSlug,
        CancellationToken cancellationToken)
    {
        var slugByAddress = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var address in contractAddresses)
        {
            var response = await client.GetInfoByAddressAsync(address, cancellationToken);
            if (CoinMarketCapErrorMapper.RejectsParameter(response, "address"))
            {
                return ServiceResult<IReadOnlyDictionary<string, string>>.Failure(ServiceErrorCode.NotFound);
            }

            if (!response.IsSuccess)
            {
                return ServiceResult<IReadOnlyDictionary<string, string>>.Failure(CoinMarketCapErrorMapper.Map(response));
            }

            var slug = (response.Value?.Values ?? [])
                .FirstOrDefault(coin => coin?.Slug is not null && ListsAddress(coin, address, platformSlug))
                ?.Slug;
            if (slug is null)
            {
                return ServiceResult<IReadOnlyDictionary<string, string>>.Failure(ServiceErrorCode.NotFound);
            }

            slugByAddress[address] = slug;
        }

        return ServiceResult<IReadOnlyDictionary<string, string>>.Success(slugByAddress);
    }

    private static bool ListsAddress(CoinMarketCapCoinInfo coin, string address, string platformSlug) =>
        (coin.ContractAddresses ?? []).Any(contract =>
            string.Equals(contract?.Address, address, StringComparison.OrdinalIgnoreCase)
            && string.Equals(contract?.Platform?.Coin?.Slug, platformSlug, StringComparison.Ordinal));
}
