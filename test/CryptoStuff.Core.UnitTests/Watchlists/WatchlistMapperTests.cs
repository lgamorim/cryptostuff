using CryptoStuff.Core.UnitTests.TestSupport;
using CryptoStuff.Core.Watchlists;

namespace CryptoStuff.Core.UnitTests.Watchlists;

public class WatchlistMapperTests
{
    [Fact]
    public void Should_MapIdNameAndCoinsInOrder_When_WatchlistMapped()
    {
        var watchlist = new FakeWatchlistRepository().Seed(Watchlist.Create("Majors"));
        watchlist.AddCoin("ethereum");
        watchlist.AddCoin("bitcoin");

        var view = WatchlistMapper.ToView(watchlist);

        view.Id.Should().Be(watchlist.Id);
        view.Name.Should().Be("Majors");
        view.CoinIds.Should().Equal("ethereum", "bitcoin");
    }

    [Fact]
    public void Should_KeepViewUnchanged_When_WatchlistChangedAfterMapping()
    {
        var watchlist = Watchlist.Create("Majors");
        watchlist.AddCoin("bitcoin");
        var view = WatchlistMapper.ToView(watchlist);

        watchlist.AddCoin("ethereum");

        view.CoinIds.Should().Equal("bitcoin");
    }
}
