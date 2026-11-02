using System.Globalization;

namespace CryptoStuff.Core.UnitTests;

public class TimestampFormatValidatorTests
{
    [Fact]
    public void Should_ReturnValid_When_TimestampIsUtcWithZSuffix()
    {
        var outcome = TimestampFormatValidator.Validate("2024-01-31T14:30:00Z", "OccurredAt");

        outcome.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("2024-01-31T16:30:00+02:00")]
    [InlineData("2024-01-31T09:30:00-05:00")]
    [InlineData("2024-01-31T14:30:00+00:00")]
    public void Should_ReturnValid_When_TimestampHasNumericOffset(string value)
    {
        var outcome = TimestampFormatValidator.Validate(value, "OccurredAt");

        outcome.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("2024-01-31T14:30:00.5Z")]
    [InlineData("2024-01-31T14:30:00.1234567Z")]
    [InlineData("2024-01-31T16:30:00.250+02:00")]
    public void Should_ReturnValid_When_TimestampHasFractionalSeconds(string value)
    {
        var outcome = TimestampFormatValidator.Validate(value, "OccurredAt");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_TimestampHasNoOffset()
    {
        var outcome = TimestampFormatValidator.Validate("2024-01-31T14:30:00", "OccurredAt");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("OccurredAt");
    }

    [Fact]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_ValueIsNull()
    {
        var outcome = TimestampFormatValidator.Validate(null, "OccurredAt");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("OccurredAt");
    }

    [Fact]
    public void Should_ReturnValid_When_TimestampIsLeapDayOnLeapYear()
    {
        var outcome = TimestampFormatValidator.Validate("2024-02-29T00:00:00Z", "OccurredAt");

        outcome.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2024-01-31")]
    [InlineData("2024-01-31 14:30:00Z")]
    [InlineData("2024-01-31T14:30Z")]
    [InlineData("31-01-2024T14:30:00Z")]
    [InlineData("01/31/2024 14:30:00Z")]
    [InlineData("2023-02-29T00:00:00Z")]
    [InlineData("2024-01-31T24:00:00Z")]
    [InlineData("2024-01-31T14:30:00.12345678Z")]
    [InlineData(" 2024-01-31T14:30:00Z")]
    [InlineData("2024-01-31T14:30:00Z ")]
    [InlineData("not a timestamp")]
    public void Should_ReturnInvalid_When_ValueIsNotAnIso8601TimestampWithOffset(string value)
    {
        var outcome = TimestampFormatValidator.Validate(value, "OccurredAt");

        outcome.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_ParseToSameInstantWithZeroOffset_When_TryParseGivenNumericOffset()
    {
        var parsed = TimestampFormatValidator.TryParse("2024-01-31T16:30:00.25+02:00", out var timestamp);

        parsed.Should().BeTrue();
        timestamp.Should().Be(new DateTimeOffset(2024, 1, 31, 14, 30, 0, 250, TimeSpan.Zero));
        timestamp.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Should_ParseAsUtc_When_TryParseGivenZSuffix()
    {
        var parsed = TimestampFormatValidator.TryParse("2024-01-31T14:30:00Z", out var timestamp);

        parsed.Should().BeTrue();
        timestamp.Should().Be(new DateTimeOffset(2024, 1, 31, 14, 30, 0, TimeSpan.Zero));
        timestamp.Offset.Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("2024-01-31T14:30:00")]
    public void Should_ReturnFalse_When_TryParseGivenInvalidTimestamp(string? value)
    {
        var parsed = TimestampFormatValidator.TryParse(value, out _);

        parsed.Should().BeFalse();
    }

    [Fact]
    public void Should_UseInvariantCultureParsing_When_CurrentCultureIsNonInvariant()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
        CultureInfo.CurrentUICulture = new CultureInfo("ar-SA");
        try
        {
            var outcome = TimestampFormatValidator.Validate("2024-01-31T14:30:00Z", "OccurredAt");

            outcome.IsValid.Should().BeTrue();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
