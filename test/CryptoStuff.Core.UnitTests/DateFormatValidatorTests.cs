using System.Globalization;

namespace CryptoStuff.Core.UnitTests;

public class DateFormatValidatorTests
{
    [Fact]
    public void Should_ReturnValid_When_DateIsWellFormed()
    {
        var outcome = DateFormatValidator.Validate("01-01-2024", "Date");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnValid_When_DateIsLeapDayOnLeapYear()
    {
        var outcome = DateFormatValidator.Validate("29-02-2024", "Date");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_ValueIsNull()
    {
        var outcome = DateFormatValidator.Validate(null, "Date");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("Date");
    }

    [Fact]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_ValueIsEmpty()
    {
        var outcome = DateFormatValidator.Validate(string.Empty, "Date");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("Date");
    }

    [Fact]
    public void Should_ReturnInvalid_When_SeparatorsAreWrong()
    {
        var outcome = DateFormatValidator.Validate("01/01/2024", "Date");

        outcome.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_ReturnInvalid_When_ComponentOrderIsWrong()
    {
        var outcome = DateFormatValidator.Validate("2024-01-01", "Date");

        outcome.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_ReturnInvalid_When_DayExceedsMonthRange()
    {
        var outcome = DateFormatValidator.Validate("32-01-2024", "Date");

        outcome.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_ReturnInvalid_When_DateIsLeapDayOnNonLeapYear()
    {
        var outcome = DateFormatValidator.Validate("29-02-2023", "Date");

        outcome.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_ReturnInvalid_When_ValueHasLeadingOrTrailingWhitespace()
    {
        var outcome = DateFormatValidator.Validate(" 01-01-2024", "Date");

        outcome.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_UseInvariantCultureParsing_When_CurrentCultureIsNonInvariant()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
        try
        {
            var outcome = DateFormatValidator.Validate("01-01-2024", "Date");

            outcome.IsValid.Should().BeTrue();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
