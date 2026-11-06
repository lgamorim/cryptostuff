using System.Text.Json;
using CryptoStuff.Core.Alerts;

namespace CryptoStuff.Core.UnitTests.Alerts;

public class AlertStateParserTests
{
    [Fact]
    public void Should_ParseArmed_When_TokenIsArmed()
    {
        var parsed = AlertStateParser.TryParse("armed", out var state);

        parsed.Should().BeTrue();
        state.Should().Be(AlertState.Armed);
    }

    [Fact]
    public void Should_ParseTriggered_When_TokenIsTriggered()
    {
        var parsed = AlertStateParser.TryParse("triggered", out var state);

        parsed.Should().BeTrue();
        state.Should().Be(AlertState.Triggered);
    }

    [Theory]
    [InlineData("ARMED", AlertState.Armed)]
    [InlineData("Triggered", AlertState.Triggered)]
    public void Should_IgnoreCase_When_TokenDiffersOnlyInCase(string token, AlertState expected)
    {
        var parsed = AlertStateParser.TryParse(token, out var state);

        parsed.Should().BeTrue();
        state.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("disarmed")]
    [InlineData("fired")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData(" armed")]
    [InlineData("triggered ")]
    public void Should_ReturnFalse_When_TokenIsNotAState(string? token)
    {
        var parsed = AlertStateParser.TryParse(token, out _);

        parsed.Should().BeFalse();
    }

    [Fact]
    public void Should_ReturnValid_When_ValidateGivenStateToken()
    {
        var outcome = AlertStateParser.Validate("armed", "State");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnInvalidNamingFieldAndAcceptedTokens_When_ValidateGivenUnknownToken()
    {
        var outcome = AlertStateParser.Validate("fired", "State");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("State").And.Contain("armed").And.Contain("triggered");
    }

    // The hosts print an enum with JsonNamingPolicy.KebabCaseLower, so every
    // name printed that way has to parse back to the same state.
    [Fact]
    public void Should_ParseEveryStateFromItsKebabCaseName_When_NameIsPrintedTheWayHostsPrintIt()
    {
        foreach (var state in Enum.GetValues<AlertState>())
        {
            var token = JsonNamingPolicy.KebabCaseLower.ConvertName(state.ToString());

            AlertStateParser.TryParse(token, out var parsed).Should().BeTrue(token);
            parsed.Should().Be(state);
        }
    }
}
