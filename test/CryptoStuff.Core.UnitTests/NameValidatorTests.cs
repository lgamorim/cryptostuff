namespace CryptoStuff.Core.UnitTests;

public class NameValidatorTests
{
    [Fact]
    public void Should_ReturnValid_When_NameIsNonBlank()
    {
        var outcome = NameValidator.Validate("Majors", "Name");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnValid_When_NameIsExactlyMaxLength()
    {
        var outcome = NameValidator.Validate(new string('a', NameValidator.MaxLength), "Name");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_NameIsNull()
    {
        var outcome = NameValidator.Validate(null, "Name");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("Name");
    }

    [Fact]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_NameIsEmpty()
    {
        var outcome = NameValidator.Validate(string.Empty, "Name");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("Name");
    }

    [Fact]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_NameIsWhitespaceOnly()
    {
        var outcome = NameValidator.Validate("   ", "Name");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("Name");
    }

    [Fact]
    public void Should_ReturnInvalidWithFieldNameAndLimitInMessage_When_NameExceedsMaxLength()
    {
        var outcome = NameValidator.Validate(new string('a', NameValidator.MaxLength + 1), "Name");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("Name").And.Contain("100");
    }
}
