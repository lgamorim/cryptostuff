namespace CryptoStuff.Core.UnitTests;

public class ValidationOutcomeTests
{
    [Fact]
    public void Should_CarryValidWithNoErrorMessage_When_CreatedViaSuccess()
    {
        var outcome = ValidationOutcome.Success();

        outcome.IsValid.Should().BeTrue();
        outcome.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Should_CarryInvalidWithErrorMessage_When_CreatedViaFailure()
    {
        var outcome = ValidationOutcome.Failure("CoinId is required.");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Be("CoinId is required.");
    }

    [Fact]
    public void Should_ThrowArgumentException_When_CreatedViaFailureWithNullMessage()
    {
        var act = () => ValidationOutcome.Failure(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_ThrowArgumentException_When_CreatedViaFailureWithWhitespaceOnlyMessage()
    {
        var act = () => ValidationOutcome.Failure("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_ThrowArgumentException_When_CreatedViaFailureWithEmptyMessage()
    {
        var act = () => ValidationOutcome.Failure(string.Empty);

        act.Should().Throw<ArgumentException>();
    }
}
