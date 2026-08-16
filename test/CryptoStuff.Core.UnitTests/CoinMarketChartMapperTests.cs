using System.Globalization;
using CryptoStuff.CoinGecko;

namespace CryptoStuff.Core.UnitTests;

public class CoinMarketChartMapperTests
{
    [Fact]
    public void Should_MapUnixMillisecondTimestampToInvariantCultureDateString_When_MappingAPricePoint()
    {
        var chart = new CoinGeckoMarketChart
        {
            Prices = [new CoinGeckoMarketChartPoint(1704067200000, 42000m)],
            MarketCaps = [],
            TotalVolumes = [],
        };

        var view = CoinMarketChartMapper.ToView(chart);

        view.Prices.Should().ContainSingle();
        view.Prices[0].Date.Should().Be("2024-01-01");
        view.Prices[0].Value.Should().Be(42000m);
    }

    [Fact]
    public void Should_FormatDateAsUtc_When_TimestampIsNearALocalMidnightBoundary()
    {
        // 2024-01-01T00:30:00Z — thirty minutes past UTC midnight. A `.LocalDateTime`
        // or unqualified `.Date` conversion would land on 2023-12-31 in any timezone
        // west of UTC, making this test's result depend on the machine's local zone.
        var chart = new CoinGeckoMarketChart
        {
            Prices = [new CoinGeckoMarketChartPoint(1704069000000, 1m)],
            MarketCaps = [],
            TotalVolumes = [],
        };

        var view = CoinMarketChartMapper.ToView(chart);

        view.Prices[0].Date.Should().Be("2024-01-01");
    }

    [Fact]
    public void Should_UseInvariantCultureDateFormat_When_CurrentThreadCultureIsNonInvariant()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
        try
        {
            var chart = new CoinGeckoMarketChart
            {
                Prices = [new CoinGeckoMarketChartPoint(1704067200000, 1m)],
                MarketCaps = [],
                TotalVolumes = [],
            };

            var view = CoinMarketChartMapper.ToView(chart);

            view.Prices[0].Date.Should().Be("2024-01-01");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void Should_MapAllThreeSeries_When_ChartHasPricesMarketCapsAndTotalVolumes()
    {
        var chart = new CoinGeckoMarketChart
        {
            Prices = [new CoinGeckoMarketChartPoint(1704067200000, 1m)],
            MarketCaps = [new CoinGeckoMarketChartPoint(1704067200000, 2m)],
            TotalVolumes = [new CoinGeckoMarketChartPoint(1704067200000, 3m)],
        };

        var view = CoinMarketChartMapper.ToView(chart);

        view.Prices.Should().ContainSingle(p => p.Value == 1m);
        view.MarketCaps.Should().ContainSingle(p => p.Value == 2m);
        view.TotalVolumes.Should().ContainSingle(p => p.Value == 3m);
    }

    [Fact]
    public void Should_PreserveSourceOrder_When_MappingMultiplePoints()
    {
        var chart = new CoinGeckoMarketChart
        {
            Prices =
            [
                new CoinGeckoMarketChartPoint(1704067200000, 1m),
                new CoinGeckoMarketChartPoint(1704153600000, 2m),
            ],
            MarketCaps = [],
            TotalVolumes = [],
        };

        var view = CoinMarketChartMapper.ToView(chart);

        view.Prices.Select(p => p.Value).Should().Equal(1m, 2m);
        view.Prices.Select(p => p.Date).Should().Equal("2024-01-01", "2024-01-02");
    }

    [Fact]
    public void Should_ReturnEmptySeries_When_SourceSeriesIsEmpty()
    {
        var chart = new CoinGeckoMarketChart
        {
            Prices = [],
            MarketCaps = [],
            TotalVolumes = [],
        };

        var view = CoinMarketChartMapper.ToView(chart);

        view.Prices.Should().BeEmpty();
        view.MarketCaps.Should().BeEmpty();
        view.TotalVolumes.Should().BeEmpty();
    }

    [Fact]
    public void Should_ReturnEmptySeries_When_ASeriesIsExplicitlyNullDespiteBeingRequired()
    {
        var chart = new CoinGeckoMarketChart { Prices = null!, MarketCaps = null!, TotalVolumes = null! };

        var view = CoinMarketChartMapper.ToView(chart);

        view.Prices.Should().BeEmpty();
        view.MarketCaps.Should().BeEmpty();
        view.TotalVolumes.Should().BeEmpty();
    }
}
