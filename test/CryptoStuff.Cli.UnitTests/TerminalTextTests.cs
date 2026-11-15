using CryptoStuff.Cli.Portfolios;
using CryptoStuff.Cli.Watchlists;
using CryptoStuff.Core.Portfolios;
using CryptoStuff.Core.Watchlists;

namespace CryptoStuff.Cli.UnitTests;

public class TerminalTextTests
{
    // An ANSI sequence that clears the screen when a terminal prints it raw.
    private const string ClearScreen = "\u001b[2J";

    [Theory]
    [InlineData("Red\u001b[31m", "Red\\u001b[31m")]
    [InlineData("Tab\there", "Tab\\u0009here")]
    [InlineData("Line\nbreak", "Line\\u000abreak")]
    [InlineData("Delete\u007f", "Delete\\u007f")]
    [InlineData("Next\u0085line", "Next\\u0085line")]
    public void Should_EscapeEachControlCharacter_When_TextHoldsOne(string text, string expected)
    {
        var escaped = TerminalText.Escape(text);

        escaped.Should().Be(expected);
    }

    [Theory]
    [InlineData("Majors")]
    [InlineData("Ação & Co. \\ 50%")]
    [InlineData("")]
    public void Should_ReturnTextUnchanged_When_ItHoldsNoControlCharacter(string text)
    {
        var escaped = TerminalText.Escape(text);

        escaped.Should().Be(text);
    }

    [Fact]
    public void Should_EscapeControlCharactersInTheName_When_FormattingAWatchlistAsText()
    {
        var view = new WatchlistView { Id = 1, Name = $"Majors{ClearScreen}", CoinIds = ["bitcoin"] };

        var single = WatchlistOutputFormatter.Format(view, json: false);
        var list = WatchlistOutputFormatter.Format([view], json: false);

        single.Should().Be("Id: 1\nName: Majors\\u001b[2J\nCoins: bitcoin");
        list.Should().Be("1: Majors\\u001b[2J (bitcoin)");
    }

    [Fact]
    public void Should_EscapeControlCharactersInTheName_When_FormattingAPortfolioAsText()
    {
        var view = new PortfolioView
        {
            Id = 1,
            Name = $"Main{ClearScreen}",
            VsCurrency = "usd",
            CostBasisMethod = CostBasisMethod.Fifo,
            Version = 1,
            Transactions = [],
        };

        var single = PortfolioOutputFormatter.Format(view, json: false);
        var list = PortfolioOutputFormatter.Format([view], json: false);

        single.Should().StartWith("Id: 1\nName: Main\\u001b[2J\n");
        list.Should().Be("1: Main\\u001b[2J (usd, fifo, 0 transactions)");
    }

    [Fact]
    public void Should_LeaveTheNameToTheSerializer_When_FormattingAsJson()
    {
        var view = new WatchlistView { Id = 1, Name = $"Majors{ClearScreen}", CoinIds = [] };

        var json = WatchlistOutputFormatter.Format(view, json: true);

        json.Should().Contain("\"Name\": \"Majors\\u001B[2J\"").And.NotContain("\u001b");
    }
}
