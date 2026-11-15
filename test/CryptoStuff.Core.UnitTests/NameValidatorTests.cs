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

    [Fact]
    public void Should_ReturnValid_When_NameIsMaxLengthOnceOuterWhitespaceIsTrimmed()
    {
        var outcome = NameValidator.Validate($"  {new string('a', NameValidator.MaxLength)}\t", "Name");

        outcome.IsValid.Should().BeTrue("the outer whitespace is trimmed before the name is stored");
    }

    [Theory]
    [InlineData("Red\u001b[31m")]
    [InlineData("Tab\there")]
    [InlineData("Line\nbreak")]
    [InlineData("Delete\u007f")]
    [InlineData("Next\u0085line")]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_NameContainsAControlCharacter(string name)
    {
        var outcome = NameValidator.Validate(name, "Name");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Be("Name must not contain control characters.");
    }

    [Fact]
    public void Should_ReturnNameWithoutOuterWhitespace_When_Normalized()
    {
        var normalized = NameValidator.Normalize(" \tMajors  ");

        normalized.Should().Be("Majors");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("Red\u001b[31m")]
    public void Should_ThrowArgumentException_When_NormalizingANameThatFailsValidation(string? name)
    {
        // `!` passes null past the non-nullable parameter to exercise the guard.
        var act = () => NameValidator.Normalize(name!);

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }
}
