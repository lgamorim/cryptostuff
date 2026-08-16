namespace CryptoStuff.Core.UnitTests;

public class CoinPriceMapperTests
{
    [Fact]
    public void Should_MapMatrixToPerCoinPriceLists_When_AllRequestedCoinIdsArePresent()
    {
        var matrix = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["bitcoin"] = new() { ["usd"] = 50000m },
            ["ethereum"] = new() { ["usd"] = 3000m },
        };

        var view = CoinPriceMapper.ToView(matrix, ["bitcoin", "ethereum"], ["usd"]);

        view.Coins.Should().HaveCount(2);
        view.Coins[0].CoinId.Should().Be("bitcoin");
        view.Coins[0].Amounts.Should().ContainSingle(a => a.Currency == "usd" && a.Amount == 50000m);
        view.Coins[1].CoinId.Should().Be("ethereum");
        view.Coins[1].Amounts.Should().ContainSingle(a => a.Currency == "usd" && a.Amount == 3000m);
    }

    [Fact]
    public void Should_PreserveRequestedCoinOrder_When_MappingMultipleCoins()
    {
        var matrix = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["ethereum"] = new() { ["usd"] = 3000m },
            ["bitcoin"] = new() { ["usd"] = 50000m },
        };

        var view = CoinPriceMapper.ToView(matrix, ["bitcoin", "ethereum"], ["usd"]);

        view.Coins.Select(c => c.CoinId).Should().Equal("bitcoin", "ethereum");
    }

    [Fact]
    public void Should_PreserveRequestedCurrencyOrder_When_MappingAmounts()
    {
        var matrix = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["bitcoin"] = new() { ["eur"] = 45000m, ["usd"] = 50000m },
        };

        var view = CoinPriceMapper.ToView(matrix, ["bitcoin"], ["usd", "eur"]);

        view.Coins[0].Amounts.Select(a => a.Currency).Should().Equal("usd", "eur");
    }

    [Fact]
    public void Should_OmitCurrencyFromAmounts_When_CurrencyMissingForAPresentCoin()
    {
        var matrix = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["bitcoin"] = new() { ["usd"] = 50000m },
        };

        var view = CoinPriceMapper.ToView(matrix, ["bitcoin"], ["usd", "eur"]);

        view.Coins[0].Amounts.Should().ContainSingle();
        view.Coins[0].Amounts[0].Currency.Should().Be("usd");
    }

    [Fact]
    public void Should_ReturnEmptyCoinsList_When_NoCoinIdsRequested()
    {
        var matrix = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["bitcoin"] = new() { ["usd"] = 50000m },
        };

        var view = CoinPriceMapper.ToView(matrix, [], ["usd"]);

        view.Coins.Should().BeEmpty();
    }

    [Fact]
    public void Should_TreatCoinAsHavingNoAmounts_When_MatrixValueForThatCoinIsExplicitlyNull()
    {
        var matrix = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["bitcoin"] = null!,
        };

        var view = CoinPriceMapper.ToView(matrix, ["bitcoin"], ["usd"]);

        view.Coins.Should().ContainSingle();
        view.Coins[0].Amounts.Should().BeEmpty();
    }
}
