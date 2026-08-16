namespace CryptoStuff.Core.UnitTests;

public class PositiveDayCountValidatorTests
{
    [Fact]
    public void Should_ReturnValid_When_DaysIsOne()
    {
        var outcome = PositiveDayCountValidator.Validate(1, "Days");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnValid_When_DaysIsLargePositiveNumber()
    {
        var outcome = PositiveDayCountValidator.Validate(365, "Days");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_DaysIsZero()
    {
        var outcome = PositiveDayCountValidator.Validate(0, "Days");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("Days");
    }

    [Fact]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_DaysIsNegative()
    {
        var outcome = PositiveDayCountValidator.Validate(-1, "Days");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("Days");
    }
}
