using System.Globalization;

namespace CryptoStuff.Core.UnitTests;

public class NonNegativeDecimalValidatorTests
{
    [Fact]
    public void Should_ReturnValid_When_ValueIsZero()
    {
        var outcome = NonNegativeDecimalValidator.Validate(0m, "Fee");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnValid_When_ValueIsMaxValue()
    {
        var outcome = NonNegativeDecimalValidator.Validate(decimal.MaxValue, "Fee");

        outcome.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("-0.0000000000000000000000000001")]
    [InlineData("-1")]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_ValueIsNegative(string value)
    {
        var outcome = NonNegativeDecimalValidator.Validate(decimal.Parse(value, CultureInfo.InvariantCulture), "Fee");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("Fee");
    }
}
