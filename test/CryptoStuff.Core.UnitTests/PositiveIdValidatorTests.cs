namespace CryptoStuff.Core.UnitTests;

public class PositiveIdValidatorTests
{
    [Fact]
    public void Should_ReturnValid_When_IdIsOne()
    {
        var outcome = PositiveIdValidator.Validate(1, "Id");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnValid_When_IdIsMaxValue()
    {
        var outcome = PositiveIdValidator.Validate(int.MaxValue, "Id");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_IdIsZero()
    {
        var outcome = PositiveIdValidator.Validate(0, "Id");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("Id");
    }

    [Fact]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_IdIsNegative()
    {
        var outcome = PositiveIdValidator.Validate(-1, "Id");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("Id");
    }
}
