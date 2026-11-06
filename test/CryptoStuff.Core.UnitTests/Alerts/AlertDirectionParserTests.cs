using System.Text.Json;
using CryptoStuff.Core.Alerts;

namespace CryptoStuff.Core.UnitTests.Alerts;

public class AlertDirectionParserTests
{
    [Fact]
    public void Should_ParseAbove_When_TokenIsAbove()
    {
        var parsed = AlertDirectionParser.TryParse("above", out var direction);

        parsed.Should().BeTrue();
        direction.Should().Be(AlertDirection.Above);
    }

    [Fact]
    public void Should_ParseBelow_When_TokenIsBelow()
    {
        var parsed = AlertDirectionParser.TryParse("below", out var direction);

        parsed.Should().BeTrue();
        direction.Should().Be(AlertDirection.Below);
    }

    [Theory]
    [InlineData("ABOVE", AlertDirection.Above)]
    [InlineData("Below", AlertDirection.Below)]
    public void Should_IgnoreCase_When_TokenDiffersOnlyInCase(string token, AlertDirection expected)
    {
        var parsed = AlertDirectionParser.TryParse(token, out var direction);

        parsed.Should().BeTrue();
        direction.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("up")]
    [InlineData("over")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData(" above")]
    [InlineData("below ")]
    public void Should_ReturnFalse_When_TokenIsNotADirection(string? token)
    {
        var parsed = AlertDirectionParser.TryParse(token, out _);

        parsed.Should().BeFalse();
    }

    [Fact]
    public void Should_ReturnValid_When_ValidateGivenDirectionToken()
    {
        var outcome = AlertDirectionParser.Validate("above", "Direction");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnInvalidNamingFieldAndAcceptedTokens_When_ValidateGivenUnknownToken()
    {
        var outcome = AlertDirectionParser.Validate("sideways", "Direction");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("Direction").And.Contain("above").And.Contain("below");
    }

    // The hosts print an enum with JsonNamingPolicy.KebabCaseLower, so every
    // name printed that way has to parse back to the same direction.
    [Fact]
    public void Should_ParseEveryDirectionFromItsKebabCaseName_When_NameIsPrintedTheWayHostsPrintIt()
    {
        foreach (var direction in Enum.GetValues<AlertDirection>())
        {
            var token = JsonNamingPolicy.KebabCaseLower.ConvertName(direction.ToString());

            AlertDirectionParser.TryParse(token, out var parsed).Should().BeTrue(token);
            parsed.Should().Be(direction);
        }
    }
}
