namespace CryptoStuff.Core.UnitTests;

public class NonEmptyScalarValidatorTests
{
    [Fact]
    public void Should_ReturnValid_When_ValueIsNonEmptyString()
    {
        var outcome = NonEmptyScalarValidator.Validate("bitcoin", "CoinId");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnValid_When_ValueHasLeadingOrTrailingWhitespaceButHasContent()
    {
        var outcome = NonEmptyScalarValidator.Validate(" btc ", "CoinId");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_ValueIsNull()
    {
        var outcome = NonEmptyScalarValidator.Validate(null, "CoinId");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("CoinId");
    }

    [Fact]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_ValueIsEmpty()
    {
        var outcome = NonEmptyScalarValidator.Validate(string.Empty, "CoinId");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("CoinId");
    }

    [Fact]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_ValueIsWhitespaceOnly()
    {
        var outcome = NonEmptyScalarValidator.Validate("   ", "CoinId");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("CoinId");
    }
}
