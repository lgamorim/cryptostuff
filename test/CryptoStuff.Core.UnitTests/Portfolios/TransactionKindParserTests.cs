using System.Text.Json;
using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Core.UnitTests.Portfolios;

public class TransactionKindParserTests
{
    [Fact]
    public void Should_ParseBuy_When_TokenIsBuy()
    {
        var parsed = TransactionKindParser.TryParse("buy", out var kind);

        parsed.Should().BeTrue();
        kind.Should().Be(TransactionKind.Buy);
    }

    [Fact]
    public void Should_ParseSell_When_TokenIsSell()
    {
        var parsed = TransactionKindParser.TryParse("sell", out var kind);

        parsed.Should().BeTrue();
        kind.Should().Be(TransactionKind.Sell);
    }

    [Theory]
    [InlineData("BUY", TransactionKind.Buy)]
    [InlineData("Sell", TransactionKind.Sell)]
    public void Should_IgnoreCase_When_TokenDiffersOnlyInCase(string token, TransactionKind expected)
    {
        var parsed = TransactionKindParser.TryParse(token, out var kind);

        parsed.Should().BeTrue();
        kind.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("transfer")]
    [InlineData("bought")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData(" buy")]
    [InlineData("sell ")]
    public void Should_ReturnFalse_When_TokenIsNotAKind(string? token)
    {
        var parsed = TransactionKindParser.TryParse(token, out _);

        parsed.Should().BeFalse();
    }

    // The hosts print an enum with JsonNamingPolicy.KebabCaseLower, so every
    // name printed that way has to parse back to the same kind.
    [Fact]
    public void Should_ParseEveryKindFromItsKebabCaseName_When_NameIsPrintedTheWayHostsPrintIt()
    {
        foreach (var kind in Enum.GetValues<TransactionKind>())
        {
            var token = JsonNamingPolicy.KebabCaseLower.ConvertName(kind.ToString());

            TransactionKindParser.TryParse(token, out var parsed).Should().BeTrue(token);
            parsed.Should().Be(kind);
        }
    }

    [Fact]
    public void Should_ReturnValid_When_ValidateGivenKindToken()
    {
        var outcome = TransactionKindParser.Validate("sell", "Kind");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnInvalidNamingFieldAndAcceptedTokens_When_ValidateGivenUnknownToken()
    {
        var outcome = TransactionKindParser.Validate("transfer", "Kind");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("Kind").And.Contain("buy").And.Contain("sell");
    }
}
