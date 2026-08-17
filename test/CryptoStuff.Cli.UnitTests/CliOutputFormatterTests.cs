using System.Text.Json;
using CryptoStuff.Core;

namespace CryptoStuff.Cli.UnitTests;

public class CliOutputFormatterTests
{
    [Fact]
    public void Should_FormatCoinPricesAsText_When_JsonFalse()
    {
        var view = new CoinPriceView
        {
            Coins =
            [
                new CoinPrice
                {
                    CoinId = "bitcoin",
                    Amounts =
                    [
                        new CurrencyAmount { Currency = "usd", Amount = 43000.5m },
                        new CurrencyAmount { Currency = "eur", Amount = 39000.25m },
                    ],
                },
            ],
        };

        var result = CliOutputFormatter.Format(view, json: false);

        result.Should().Be("bitcoin: 43000.5 usd, 39000.25 eur");
    }

    [Fact]
    public void Should_FormatCoinPricesAsJson_When_JsonTrue()
    {
        var view = new CoinPriceView
        {
            Coins = [new CoinPrice { CoinId = "bitcoin", Amounts = [new CurrencyAmount { Currency = "usd", Amount = 43000.5m }] }],
        };

        var result = CliOutputFormatter.Format(view, json: true);

        var deserialized = JsonSerializer.Deserialize<CoinPriceView>(result);
        deserialized.Should().BeEquivalentTo(view);
    }

    [Fact]
    public void Should_FormatTokenPricesAsText_When_JsonFalse()
    {
        var view = new TokenPriceView
        {
            Tokens = [new TokenPrice { ContractAddress = "0xabc", Amounts = [new CurrencyAmount { Currency = "usd", Amount = 1.23m }] }],
        };

        var result = CliOutputFormatter.Format(view, json: false);

        result.Should().Be("0xabc: 1.23 usd");
    }

    [Fact]
    public void Should_FormatTokenPricesAsJson_When_JsonTrue()
    {
        var view = new TokenPriceView
        {
            Tokens = [new TokenPrice { ContractAddress = "0xabc", Amounts = [new CurrencyAmount { Currency = "usd", Amount = 1.23m }] }],
        };

        var result = CliOutputFormatter.Format(view, json: true);

        var deserialized = JsonSerializer.Deserialize<TokenPriceView>(result);
        deserialized.Should().BeEquivalentTo(view);
    }

    [Fact]
    public void Should_FormatMarketChartAsText_When_JsonFalse()
    {
        var view = new CoinMarketChartView
        {
            Prices = [new CoinMarketChartPoint { Date = "2024-01-01", Value = 42000m }],
            MarketCaps = [new CoinMarketChartPoint { Date = "2024-01-01", Value = 800000000000m }],
            TotalVolumes = [new CoinMarketChartPoint { Date = "2024-01-01", Value = 25000000000m }],
        };

        var result = CliOutputFormatter.Format(view, json: false);

        result.Should().Be(
            "Prices:\n" +
            "  2024-01-01: 42000\n" +
            "Market Caps:\n" +
            "  2024-01-01: 800000000000\n" +
            "Total Volumes:\n" +
            "  2024-01-01: 25000000000");
    }

    [Fact]
    public void Should_FormatMarketChartAsJson_When_JsonTrue()
    {
        var view = new CoinMarketChartView
        {
            Prices = [new CoinMarketChartPoint { Date = "2024-01-01", Value = 42000m }],
            MarketCaps = [],
            TotalVolumes = [],
        };

        var result = CliOutputFormatter.Format(view, json: true);

        var deserialized = JsonSerializer.Deserialize<CoinMarketChartView>(result);
        deserialized.Should().BeEquivalentTo(view);
    }

    [Fact]
    public void Should_FormatCoinWithAllFields_When_JsonFalse()
    {
        var view = new CoinView
        {
            Id = "bitcoin",
            Symbol = "btc",
            Name = "Bitcoin",
            Description = "A decentralized currency.",
            ImageUrl = "https://example.com/bitcoin.png",
            MarketData = [new CoinCurrencySnapshot { Currency = "usd", Price = 43000m, MarketCap = 800000000000m, Volume = 25000000000m }],
        };

        var result = CliOutputFormatter.Format(view, json: false);

        result.Should().Be(
            "Id: bitcoin\n" +
            "Symbol: btc\n" +
            "Name: Bitcoin\n" +
            "Description: A decentralized currency.\n" +
            "Image: https://example.com/bitcoin.png\n" +
            "Market data:\n" +
            "  usd: price=43000, marketCap=800000000000, volume=25000000000");
    }

    [Fact]
    public void Should_FormatCoinWithMissingOptionalFields_When_FieldsNull()
    {
        var view = new CoinView { Id = "bitcoin", Symbol = "btc", Name = "Bitcoin" };

        var result = CliOutputFormatter.Format(view, json: false);

        result.Should().Be(
            "Id: bitcoin\n" +
            "Symbol: btc\n" +
            "Name: Bitcoin\n" +
            "Description: (unavailable)\n" +
            "Image: (unavailable)\n" +
            "Market data: (unavailable)");
    }

    [Fact]
    public void Should_FormatCoinAsJson_When_JsonTrue()
    {
        var view = new CoinView { Id = "bitcoin", Symbol = "btc", Name = "Bitcoin", Description = "desc" };

        var result = CliOutputFormatter.Format(view, json: true);

        var deserialized = JsonSerializer.Deserialize<CoinView>(result);
        deserialized.Should().BeEquivalentTo(view);
    }

    [Fact]
    public void Should_FormatDeveloperDataWithAllFields_When_JsonFalse()
    {
        var view = new CoinDeveloperDataView
        {
            Id = "bitcoin",
            Symbol = "btc",
            Name = "Bitcoin",
            Forks = 1,
            Stars = 2,
            Subscribers = 3,
            TotalIssues = 4,
            ClosedIssues = 5,
            PullRequestsMerged = 6,
            PullRequestContributors = 7,
            CommitCount4Weeks = 8,
            CodeAdditionsLast4Weeks = 9,
            CodeDeletionsLast4Weeks = 10,
        };

        var result = CliOutputFormatter.Format(view, json: false);

        result.Should().Be(
            "Id: bitcoin\n" +
            "Symbol: btc\n" +
            "Name: Bitcoin\n" +
            "Forks: 1\n" +
            "Stars: 2\n" +
            "Subscribers: 3\n" +
            "Total issues: 4\n" +
            "Closed issues: 5\n" +
            "Pull requests merged: 6\n" +
            "Pull request contributors: 7\n" +
            "Commits (last 4 weeks): 8\n" +
            "Code additions (last 4 weeks): 9\n" +
            "Code deletions (last 4 weeks): 10");
    }

    [Fact]
    public void Should_FormatDeveloperDataWithMissingFields_When_FieldsNull()
    {
        var view = new CoinDeveloperDataView { Id = "bitcoin", Symbol = "btc", Name = "Bitcoin" };

        var result = CliOutputFormatter.Format(view, json: false);

        result.Should().Be(
            "Id: bitcoin\n" +
            "Symbol: btc\n" +
            "Name: Bitcoin\n" +
            "Forks: (unavailable)\n" +
            "Stars: (unavailable)\n" +
            "Subscribers: (unavailable)\n" +
            "Total issues: (unavailable)\n" +
            "Closed issues: (unavailable)\n" +
            "Pull requests merged: (unavailable)\n" +
            "Pull request contributors: (unavailable)\n" +
            "Commits (last 4 weeks): (unavailable)\n" +
            "Code additions (last 4 weeks): (unavailable)\n" +
            "Code deletions (last 4 weeks): (unavailable)");
    }

    [Fact]
    public void Should_FormatDeveloperDataAsJson_When_JsonTrue()
    {
        var view = new CoinDeveloperDataView { Id = "bitcoin", Symbol = "btc", Name = "Bitcoin", Forks = 1 };

        var result = CliOutputFormatter.Format(view, json: true);

        var deserialized = JsonSerializer.Deserialize<CoinDeveloperDataView>(result);
        deserialized.Should().BeEquivalentTo(view);
    }
}
