using CryptoStuff.Core.Watchlists;

namespace CryptoStuff.Core.UnitTests.Watchlists;

public class WatchlistTests
{
    // Create.

    [Fact]
    public void Should_HaveGivenNameAndNoCoins_When_Created()
    {
        var watchlist = Watchlist.Create("Majors");

        watchlist.Name.Should().Be("Majors");
        watchlist.CoinIds.Should().BeEmpty();
    }

    [Fact]
    public void Should_HaveUnassignedId_When_Created()
    {
        var watchlist = Watchlist.Create("Majors");

        watchlist.Id.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowArgumentException_When_CreatedWithBlankName(string? name)
    {
        // `!` lets the theory pass null past the non-nullable parameter to exercise the guard.
        var act = () => Watchlist.Create(name!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_ThrowArgumentException_When_CreatedWithNameLongerThanMaxLength()
    {
        var act = () => Watchlist.Create(new string('a', NameValidator.MaxLength + 1));

        act.Should().Throw<ArgumentException>();
    }

    // Rename.

    [Fact]
    public void Should_HaveNewName_When_Renamed()
    {
        var watchlist = Watchlist.Create("Majors");

        watchlist.Rename("Large caps");

        watchlist.Name.Should().Be("Large caps");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowAndKeepName_When_RenamedToBlankName(string? name)
    {
        var watchlist = Watchlist.Create("Majors");

        // `!` lets the theory pass null past the non-nullable parameter to exercise the guard.
        var act = () => watchlist.Rename(name!);

        act.Should().Throw<ArgumentException>();
        watchlist.Name.Should().Be("Majors");
    }

    [Fact]
    public void Should_ThrowAndKeepName_When_RenamedToNameLongerThanMaxLength()
    {
        var watchlist = Watchlist.Create("Majors");

        var act = () => watchlist.Rename(new string('a', NameValidator.MaxLength + 1));

        act.Should().Throw<ArgumentException>();
        watchlist.Name.Should().Be("Majors");
    }

    // AddCoin.

    [Fact]
    public void Should_KeepCoinsInInsertionOrder_When_CoinsAdded()
    {
        var watchlist = Watchlist.Create("Majors");

        watchlist.AddCoin("ethereum");
        watchlist.AddCoin("bitcoin");
        watchlist.AddCoin("solana");

        watchlist.CoinIds.Should().Equal("ethereum", "bitcoin", "solana");
    }

    [Fact]
    public void Should_ReturnTrue_When_AddedCoinIsNew()
    {
        var watchlist = Watchlist.Create("Majors");

        var added = watchlist.AddCoin("bitcoin");

        added.Should().BeTrue();
    }

    [Fact]
    public void Should_StoreLowercasedId_When_CoinAddedWithUppercaseLetters()
    {
        var watchlist = Watchlist.Create("Majors");

        watchlist.AddCoin("BitCoin");

        watchlist.CoinIds.Should().Equal("bitcoin");
    }

    [Fact]
    public void Should_ReturnFalseAndKeepCoinsUnchanged_When_CoinAddedTwice()
    {
        var watchlist = Watchlist.Create("Majors");
        watchlist.AddCoin("bitcoin");
        watchlist.AddCoin("ethereum");

        var added = watchlist.AddCoin("bitcoin");

        added.Should().BeFalse();
        watchlist.CoinIds.Should().Equal("bitcoin", "ethereum");
    }

    [Fact]
    public void Should_ReturnFalse_When_CoinAddedAgainWithDifferentCasing()
    {
        var watchlist = Watchlist.Create("Majors");
        watchlist.AddCoin("bitcoin");

        var added = watchlist.AddCoin("BITCOIN");

        added.Should().BeFalse();
        watchlist.CoinIds.Should().Equal("bitcoin");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowAndKeepCoinsUnchanged_When_BlankCoinAdded(string? coinId)
    {
        var watchlist = Watchlist.Create("Majors");

        // `!` lets the theory pass null past the non-nullable parameter to exercise the guard.
        var act = () => watchlist.AddCoin(coinId!);

        act.Should().Throw<ArgumentException>();
        watchlist.CoinIds.Should().BeEmpty();
    }

    // RemoveCoin.

    [Fact]
    public void Should_RemoveCoinAndKeepOrderOfOthers_When_PresentCoinRemoved()
    {
        var watchlist = Watchlist.Create("Majors");
        watchlist.AddCoin("bitcoin");
        watchlist.AddCoin("ethereum");
        watchlist.AddCoin("solana");

        var removed = watchlist.RemoveCoin("ethereum");

        removed.Should().BeTrue();
        watchlist.CoinIds.Should().Equal("bitcoin", "solana");
    }

    [Fact]
    public void Should_RemoveCoin_When_RemovedWithDifferentCasing()
    {
        var watchlist = Watchlist.Create("Majors");
        watchlist.AddCoin("bitcoin");

        var removed = watchlist.RemoveCoin("Bitcoin");

        removed.Should().BeTrue();
        watchlist.CoinIds.Should().BeEmpty();
    }

    [Fact]
    public void Should_ReturnFalseAndKeepCoinsUnchanged_When_AbsentCoinRemoved()
    {
        var watchlist = Watchlist.Create("Majors");
        watchlist.AddCoin("bitcoin");

        var removed = watchlist.RemoveCoin("ethereum");

        removed.Should().BeFalse();
        watchlist.CoinIds.Should().Equal("bitcoin");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowArgumentException_When_BlankCoinRemoved(string? coinId)
    {
        var watchlist = Watchlist.Create("Majors");

        // `!` lets the theory pass null past the non-nullable parameter to exercise the guard.
        var act = () => watchlist.RemoveCoin(coinId!);

        act.Should().Throw<ArgumentException>();
    }

    // Contains.

    [Fact]
    public void Should_ReportContainedCoinIgnoringCase_When_CoinPresent()
    {
        var watchlist = Watchlist.Create("Majors");
        watchlist.AddCoin("bitcoin");

        watchlist.Contains("BITCOIN").Should().BeTrue();
        watchlist.Contains("ethereum").Should().BeFalse();
    }

    // Encapsulation.

    [Fact]
    public void Should_NotExposeMutableBackingList_When_CoinIdsRead()
    {
        var watchlist = Watchlist.Create("Majors");
        watchlist.AddCoin("bitcoin");

        var coinIds = watchlist.CoinIds;

        coinIds.Should().BeAssignableTo<IList<string>>().Which.IsReadOnly.Should().BeTrue();
    }
}
