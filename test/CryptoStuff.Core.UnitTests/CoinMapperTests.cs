using CryptoStuff.CoinGecko;

namespace CryptoStuff.Core.UnitTests;

public class CoinMapperTests
{
    private static CoinGeckoCoin CreateCoin(
        IReadOnlyDictionary<string, string>? description = null,
        CoinGeckoImage? image = null,
        CoinGeckoCoinMarketData? marketData = null) =>
        new()
        {
            Id = "bitcoin",
            Symbol = "btc",
            Name = "Bitcoin",
            Description = description,
            Image = image ?? new CoinGeckoImage(),
            MarketData = marketData,
        };

    [Fact]
    public void Should_MapIdSymbolAndName_When_MappingCoin()
    {
        var coin = CreateCoin();

        var view = CoinMapper.ToView(coin);

        view.Id.Should().Be("bitcoin");
        view.Symbol.Should().Be("btc");
        view.Name.Should().Be("Bitcoin");
    }

    [Fact]
    public void Should_SelectEnglishDescription_When_DescriptionDictionaryContainsEnKey()
    {
        var coin = CreateCoin(description: new Dictionary<string, string> { ["en"] = "A coin.", ["fr"] = "Une pièce." });

        var view = CoinMapper.ToView(coin);

        view.Description.Should().Be("A coin.");
    }

    [Fact]
    public void Should_ReturnNullDescription_When_DescriptionDictionaryIsNull()
    {
        var coin = CreateCoin(description: null);

        var view = CoinMapper.ToView(coin);

        view.Description.Should().BeNull();
    }

    [Fact]
    public void Should_ReturnNullDescription_When_DescriptionDictionaryDoesNotContainEnKey()
    {
        var coin = CreateCoin(description: new Dictionary<string, string> { ["fr"] = "Une pièce." });

        var view = CoinMapper.ToView(coin);

        view.Description.Should().BeNull();
    }

    [Fact]
    public void Should_UseLargeImageUrl_When_LargeImageIsPresent()
    {
        var coin = CreateCoin(image: new CoinGeckoImage { Large = "large.png", Small = "small.png", Thumb = "thumb.png" });

        var view = CoinMapper.ToView(coin);

        view.ImageUrl.Should().Be("large.png");
    }

    [Fact]
    public void Should_FallBackToSmallImageUrl_When_LargeImageIsAbsent()
    {
        var coin = CreateCoin(image: new CoinGeckoImage { Large = null, Small = "small.png", Thumb = "thumb.png" });

        var view = CoinMapper.ToView(coin);

        view.ImageUrl.Should().Be("small.png");
    }

    [Fact]
    public void Should_FallBackToThumbnailImageUrl_When_LargeAndSmallImagesAreAbsent()
    {
        var coin = CreateCoin(image: new CoinGeckoImage { Large = null, Small = null, Thumb = "thumb.png" });

        var view = CoinMapper.ToView(coin);

        view.ImageUrl.Should().Be("thumb.png");
    }

    [Fact]
    public void Should_ReturnNullImageUrl_When_AllImageSizesAreAbsent()
    {
        var coin = CreateCoin(image: new CoinGeckoImage { Large = null, Small = null, Thumb = null });

        var view = CoinMapper.ToView(coin);

        view.ImageUrl.Should().BeNull();
    }

    [Fact]
    public void Should_ReturnNullImageUrl_When_ImageIsExplicitlyNullDespiteBeingRequired()
    {
        var coin = new CoinGeckoCoin { Id = "bitcoin", Symbol = "btc", Name = "Bitcoin", Image = null! };

        var view = CoinMapper.ToView(coin);

        view.ImageUrl.Should().BeNull();
    }

    [Fact]
    public void Should_ReturnNullMarketData_When_CoinHasNoMarketData()
    {
        var coin = CreateCoin(marketData: null);

        var view = CoinMapper.ToView(coin);

        view.MarketData.Should().BeNull();
    }

    [Fact]
    public void Should_JoinPriceMarketCapAndVolumePerCurrency_When_MarketDataPresentWithMatchingCurrencyKeys()
    {
        var coin = CreateCoin(marketData: new CoinGeckoCoinMarketData
        {
            CurrentPrice = new Dictionary<string, decimal> { ["usd"] = 50000m },
            MarketCap = new Dictionary<string, decimal> { ["usd"] = 900000000000m },
            TotalVolume = new Dictionary<string, decimal> { ["usd"] = 30000000000m },
        });

        var view = CoinMapper.ToView(coin);

        view.MarketData.Should().ContainSingle();
        var snapshot = view.MarketData!.Single();
        snapshot.Currency.Should().Be("usd");
        snapshot.Price.Should().Be(50000m);
        snapshot.MarketCap.Should().Be(900000000000m);
        snapshot.Volume.Should().Be(30000000000m);
    }

    [Fact]
    public void Should_OnlyIncludeCurrenciesPresentInAllThreeDictionaries_When_CurrencyKeySetsMismatch()
    {
        var coin = CreateCoin(marketData: new CoinGeckoCoinMarketData
        {
            CurrentPrice = new Dictionary<string, decimal> { ["usd"] = 1m, ["eur"] = 2m },
            MarketCap = new Dictionary<string, decimal> { ["usd"] = 1m },
            TotalVolume = new Dictionary<string, decimal> { ["usd"] = 1m, ["eur"] = 2m },
        });

        var view = CoinMapper.ToView(coin);

        view.MarketData.Should().ContainSingle();
        view.MarketData!.Single().Currency.Should().Be("usd");
    }

    [Fact]
    public void Should_OrderCurrencySnapshotsAlphabetically_When_MappingMultipleCurrencies()
    {
        var coin = CreateCoin(marketData: new CoinGeckoCoinMarketData
        {
            CurrentPrice = new Dictionary<string, decimal> { ["usd"] = 1m, ["eur"] = 1m, ["aud"] = 1m },
            MarketCap = new Dictionary<string, decimal> { ["usd"] = 1m, ["eur"] = 1m, ["aud"] = 1m },
            TotalVolume = new Dictionary<string, decimal> { ["usd"] = 1m, ["eur"] = 1m, ["aud"] = 1m },
        });

        var view = CoinMapper.ToView(coin);

        view.MarketData!.Select(s => s.Currency).Should().Equal("aud", "eur", "usd");
    }

    [Fact]
    public void Should_ReturnEmptyMarketDataList_When_NoCommonCurrencyAcrossTheThreeDictionaries()
    {
        var coin = CreateCoin(marketData: new CoinGeckoCoinMarketData
        {
            CurrentPrice = new Dictionary<string, decimal> { ["usd"] = 1m },
            MarketCap = new Dictionary<string, decimal> { ["eur"] = 1m },
            TotalVolume = new Dictionary<string, decimal> { ["aud"] = 1m },
        });

        var view = CoinMapper.ToView(coin);

        view.MarketData.Should().BeEmpty();
    }

    [Fact]
    public void Should_ReturnEmptyMarketDataList_When_CurrentPriceIsExplicitlyNullDespiteBeingRequired()
    {
        var coin = CreateCoin(marketData: new CoinGeckoCoinMarketData
        {
            CurrentPrice = null!,
            MarketCap = new Dictionary<string, decimal> { ["usd"] = 1m },
            TotalVolume = new Dictionary<string, decimal> { ["usd"] = 1m },
        });

        var view = CoinMapper.ToView(coin);

        view.MarketData.Should().BeEmpty();
    }

    [Fact]
    public void Should_ReturnEmptyMarketDataList_When_MarketCapIsExplicitlyNullDespiteBeingRequired()
    {
        var coin = CreateCoin(marketData: new CoinGeckoCoinMarketData
        {
            CurrentPrice = new Dictionary<string, decimal> { ["usd"] = 1m },
            MarketCap = null!,
            TotalVolume = new Dictionary<string, decimal> { ["usd"] = 1m },
        });

        var view = CoinMapper.ToView(coin);

        view.MarketData.Should().BeEmpty();
    }

    [Fact]
    public void Should_ReturnEmptyMarketDataList_When_TotalVolumeIsExplicitlyNullDespiteBeingRequired()
    {
        var coin = CreateCoin(marketData: new CoinGeckoCoinMarketData
        {
            CurrentPrice = new Dictionary<string, decimal> { ["usd"] = 1m },
            MarketCap = new Dictionary<string, decimal> { ["usd"] = 1m },
            TotalVolume = null!,
        });

        var view = CoinMapper.ToView(coin);

        view.MarketData.Should().BeEmpty();
    }
}
