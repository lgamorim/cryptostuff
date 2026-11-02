using System.Globalization;

namespace CryptoStuff.Core.UnitTests;

public class PositiveDecimalValidatorTests
{
    [Fact]
    public void Should_ReturnValid_When_ValueIsSmallestPositiveDecimal()
    {
        var outcome = PositiveDecimalValidator.Validate(0.0000000000000000000000000001m, "Quantity");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnValid_When_ValueIsMaxValue()
    {
        var outcome = PositiveDecimalValidator.Validate(decimal.MaxValue, "Quantity");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_ValueIsZero()
    {
        var outcome = PositiveDecimalValidator.Validate(0m, "Quantity");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("Quantity");
    }

    [Theory]
    [InlineData("-0.0000000000000000000000000001")]
    [InlineData("-1")]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_ValueIsNegative(string value)
    {
        var outcome = PositiveDecimalValidator.Validate(decimal.Parse(value, CultureInfo.InvariantCulture), "Quantity");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("Quantity");
    }
}
