using CryptoStuff.Core;

namespace CryptoStuff.CoinMarketCap.UnitTests;

public class CoinMarketCapIdsTests
{
    public static TheoryData<string, string> PinnedCoinOverrides => new()
    {
        { "binancecoin", "bnb" },
        { "ripple", "xrp" },
        { "leo-token", "unus-sed-leo" },
        { "whitebit", "whitebit-token" },
        { "dai", "multi-collateral-dai" },
        { "near", "near-protocol" },
        { "avalanche-2", "avalanche" },
        { "usd1-wlfi", "usd1" },
        { "the-open-network", "gram" },
        { "global-dollar", "global-dollar-usdg" },
        { "hedera-hashgraph", "hedera" },
        { "quant-network", "quant" },
        { "crypto-com-chain", "cronos" },
        { "bitway", "bitway-btw" },
        { "htx-dao", "htx" },
        { "aster-2", "aster" },
        { "world-liberty-financial", "world-liberty-financial-wlfi" },
        { "polkadot", "polkadot-new" },
        { "bitget-token", "bitget-token-new" },
        { "worldcoin-wld", "worldcoin-org" },
        { "kucoin-shares", "kucoin-token" },
        { "jupiter-exchange-solana", "jupiter-ag" },
        { "midnight-3", "midnight-network" },
        { "render-token", "render" },
        { "gatechain-token", "gatetoken" },
        { "injective-protocol", "injective" },
        { "pi-network", "pi" },
        { "pancakeswap-token", "pancakeswap" },
        { "xdce-crowd-sale", "xdc-network" },
        { "true-usd", "trueusd" },
        { "stable-2", "stable" },
        { "blockstack", "stacks" },
    };

    public static TheoryData<string> PinnedOverrideSlugs => new(PinnedCoinOverrides.Select(row => row.Data.Item2));

    public static TheoryData<string, string> PinnedPlatforms => new()
    {
        { "ethereum", "ethereum" },
        { "binance-smart-chain", "bnb" },
        { "polygon-pos", "polygon-ecosystem-token" },
        { "arbitrum-one", "arbitrum" },
        { "optimistic-ethereum", "optimism-ethereum" },
        { "base", "base" },
        { "avalanche", "avalanche" },
        { "solana", "solana" },
        { "tron", "tron" },
    };

    // Coin id to slug.

    [Theory]
    [MemberData(nameof(PinnedCoinOverrides))]
    public void Should_TranslateCoinIdToItsListedSlug_When_OverrideListsIt(string coinId, string slug)
    {
        var result = CoinMarketCapIds.SlugOf(coinId);

        result.Should().Be(slug);
    }

    [Theory]
    [InlineData("bitcoin")]
    [InlineData("usd-coin")]
    [InlineData("a-coin-no-provider-knows")]
    public void Should_UseCoinIdAsSlug_When_NoOverrideListsIt(string coinId)
    {
        var result = CoinMarketCapIds.SlugOf(coinId);

        result.Should().Be(coinId);
    }

    [Fact]
    public void Should_UseCoinIdAsSlug_When_ListedIdDiffersOnlyInCase()
    {
        // The hosts pass an id as typed, and CoinGecko's adapter matches ids ordinally too.
        var result = CoinMarketCapIds.SlugOf("Binancecoin");

        result.Should().Be("Binancecoin");
    }

    [Theory]
    [MemberData(nameof(PinnedOverrideSlugs))]
    public void Should_TranslateToNoSlug_When_CoinIdIsAnotherCoinsOverrideSlug(string coinId)
    {
        // The table shows the slug is another CoinGecko id's coin, so this id is no coin
        // CoinGecko knows, and identity would price it as that other coin.
        var result = CoinMarketCapIds.SlugOf(coinId);

        result.Should().BeNull();
    }

    [Fact]
    public void Should_ListOnlyThePinnedOverrides_When_TableIsRead()
    {
        var pinned = PinnedCoinOverrides.Select(row => KeyValuePair.Create(row.Data.Item1, row.Data.Item2));

        CoinMarketCapIds.CoinSlugOverrides.Should().BeEquivalentTo(pinned);
    }

    [Fact]
    public void Should_MapNoTwoCoinIdsToOneSlug_When_OverridesAreListed()
    {
        var slugs = CoinMarketCapIds.CoinSlugOverrides.Values;

        slugs.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Should_ChangeEveryListedCoinId_When_OverridesAreListed()
    {
        var unchanged = CoinMarketCapIds.CoinSlugOverrides.Where(entry => entry.Key == entry.Value);

        unchanged.Should().BeEmpty();
    }

    [Fact]
    public void Should_ListNoOverrideSlugAsAListedCoinId_When_OverridesAreListed()
    {
        var both = CoinMarketCapIds.CoinSlugOverrides.Values.Intersect(CoinMarketCapIds.CoinSlugOverrides.Keys);

        both.Should().BeEmpty();
    }

    // Slug in a response back to the requested id.

    [Theory]
    [MemberData(nameof(PinnedCoinOverrides))]
    public void Should_ResolveSlugBackToRequestedCoinId_When_OverrideListsIt(string coinId, string slug)
    {
        var coinIdsBySlug = CoinMarketCapIds.RequestedCoinIdsBySlug([coinId]);

        coinIdsBySlug[slug].Should().Equal(coinId);
    }

    [Fact]
    public void Should_ResolveSlugBackToRequestedCoinId_When_NoOverrideListsIt()
    {
        var coinIdsBySlug = CoinMarketCapIds.RequestedCoinIdsBySlug(["bitcoin"]);

        coinIdsBySlug["bitcoin"].Should().Equal("bitcoin");
    }

    [Fact]
    public void Should_ResolveSlugBackToRequestedCoinId_When_ListedIdDiffersOnlyInCase()
    {
        var coinIdsBySlug = CoinMarketCapIds.RequestedCoinIdsBySlug(["Binancecoin"]);

        coinIdsBySlug.Select(group => group.Key).Should().Equal("Binancecoin");
    }

    [Fact]
    public void Should_KeepCoinIdsApart_When_TheyDifferOnlyInCase()
    {
        var coinIdsBySlug = CoinMarketCapIds.RequestedCoinIdsBySlug(["Bitcoin", "bitcoin"]);

        coinIdsBySlug.Select(group => group.Key).Should().Equal("Bitcoin", "bitcoin");
        coinIdsBySlug["Bitcoin"].Should().Equal("Bitcoin");
        coinIdsBySlug["bitcoin"].Should().Equal("bitcoin");
    }

    [Fact]
    public void Should_ListEachSlugOnceInRequestOrder_When_CoinIdsAreRequested()
    {
        var coinIdsBySlug = CoinMarketCapIds.RequestedCoinIdsBySlug(["bitcoin", "binancecoin", "ripple"]);

        coinIdsBySlug.Select(group => group.Key).Should().Equal("bitcoin", "bnb", "xrp");
    }

    [Fact]
    public void Should_ResolveSlugBackOnlyToTheOverriddenCoinId_When_AnotherCoinsOverrideSlugIsAlsoRequested()
    {
        var coinIdsBySlug = CoinMarketCapIds.RequestedCoinIdsBySlug(["binancecoin", "bnb"]);

        coinIdsBySlug.Select(group => group.Key).Should().Equal("bnb");
        coinIdsBySlug["bnb"].Should().Equal("binancecoin");
    }

    [Fact]
    public void Should_ResolveNoSlugs_When_OnlyAnotherCoinsOverrideSlugIsRequested()
    {
        var coinIdsBySlug = CoinMarketCapIds.RequestedCoinIdsBySlug(["xrp"]);

        coinIdsBySlug.Should().BeEmpty();
    }

    [Fact]
    public void Should_ResolveSlugToRequestedCoinIdOnce_When_CoinIdIsRequestedTwice()
    {
        var coinIdsBySlug = CoinMarketCapIds.RequestedCoinIdsBySlug(["bitcoin", "bitcoin"]);

        coinIdsBySlug["bitcoin"].Should().Equal("bitcoin");
    }

    [Fact]
    public void Should_ResolveNoCoinId_When_SlugWasNotRequested()
    {
        var coinIdsBySlug = CoinMarketCapIds.RequestedCoinIdsBySlug(["binancecoin"]);

        coinIdsBySlug["binancecoin"].Should().BeEmpty();
    }

    [Fact]
    public void Should_ResolveNoSlugs_When_NoCoinIdsAreRequested()
    {
        var coinIdsBySlug = CoinMarketCapIds.RequestedCoinIdsBySlug([]);

        coinIdsBySlug.Should().BeEmpty();
    }

    // Platform id to CoinMarketCap platform.

    [Theory]
    [MemberData(nameof(PinnedPlatforms))]
    public void Should_TranslatePlatformToItsListedSlug_When_TableListsIt(string platformId, string slug)
    {
        var result = CoinMarketCapIds.PlatformSlugOf(platformId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(slug);
    }

    [Theory]
    [InlineData("fantom")]
    [InlineData("polygon")]
    [InlineData("Ethereum")]
    [InlineData("")]
    public void Should_ReturnNotFound_When_TableDoesNotListPlatform(string platformId)
    {
        var result = CoinMarketCapIds.PlatformSlugOf(platformId);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public void Should_ListOnlyThePinnedPlatforms_When_TableIsRead()
    {
        var pinned = PinnedPlatforms.Select(row => KeyValuePair.Create(row.Data.Item1, row.Data.Item2));

        CoinMarketCapIds.PlatformSlugs.Should().BeEquivalentTo(pinned);
    }

    [Fact]
    public void Should_MapNoTwoPlatformsToOneSlug_When_PlatformsAreListed()
    {
        var slugs = CoinMarketCapIds.PlatformSlugs.Values;

        slugs.Should().OnlyHaveUniqueItems();
    }
}
