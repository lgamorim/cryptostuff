using System.Text.Json;

namespace CryptoStuff.Composition.UnitTests;

public class UtcTimestampJsonConverterTests
{
    // The converter is reached the way both hosts reach it.
    private static readonly JsonSerializerOptions Options = CryptoStuffJson.Configure(new JsonSerializerOptions());

    [Fact]
    public void Should_WriteZSuffixedUtc_When_ValueHasAZeroOffset()
    {
        // Arrange
        var value = new DateTimeOffset(2024, 3, 1, 12, 0, 0, TimeSpan.Zero);

        // Act
        var json = JsonSerializer.Serialize(value, Options);

        // Assert
        json.Should().Be("\"2024-03-01T12:00:00Z\"");
    }

    [Fact]
    public void Should_ConvertToUtc_When_ValueHasAnotherOffset()
    {
        // Arrange
        var value = new DateTimeOffset(2024, 1, 31, 16, 30, 0, TimeSpan.FromHours(2));

        // Act
        var json = JsonSerializer.Serialize(value, Options);

        // Assert
        json.Should().Be("\"2024-01-31T14:30:00Z\"");
    }

    [Fact]
    public void Should_KeepTheFractionOfASecond_When_ValueHasOne()
    {
        // Arrange
        var value = new DateTimeOffset(2024, 3, 1, 12, 0, 0, TimeSpan.Zero).AddTicks(1_234_500);

        // Act
        var json = JsonSerializer.Serialize(value, Options);

        // Assert
        json.Should().Be("\"2024-03-01T12:00:00.12345Z\"");
    }

    [Fact]
    public void Should_WriteNull_When_NullableValueIsNull()
    {
        // Act
        var json = JsonSerializer.Serialize<DateTimeOffset?>(null, Options);

        // Assert
        json.Should().Be("null");
    }

    [Fact]
    public void Should_WriteZSuffixedUtc_When_NullableValueIsSet()
    {
        // Arrange
        DateTimeOffset? value = new DateTimeOffset(2024, 3, 1, 12, 0, 0, TimeSpan.Zero);

        // Act
        var json = JsonSerializer.Serialize(value, Options);

        // Assert
        json.Should().Be("\"2024-03-01T12:00:00Z\"");
    }

    [Theory]
    [InlineData("\"2024-03-01T12:00:00Z\"")]
    [InlineData("\"2024-03-01T12:00:00+00:00\"")]
    [InlineData("\"2024-03-01T14:00:00+02:00\"")]
    public void Should_ReadTheSameInstant_When_TextUsesEitherOffsetForm(string json)
    {
        // Act
        var value = JsonSerializer.Deserialize<DateTimeOffset>(json, Options);

        // Assert
        value.Should().Be(new DateTimeOffset(2024, 3, 1, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Should_ReadTheValueInUtc_When_TextHasAnotherOffset()
    {
        // Act
        var value = JsonSerializer.Deserialize<DateTimeOffset>("\"2024-03-01T14:00:00+02:00\"", Options);

        // Assert
        value.Offset.Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData("\"2024-03-01T12:00:00\"")]
    [InlineData("\"2024-03-01T12:00:00.5\"")]
    [InlineData("\"2024-03-01\"")]
    [InlineData("\"2024-03-01T12:00Z\"")]
    [InlineData("\"\"")]
    [InlineData("1709294400")]
    [InlineData("null")]
    public void Should_Throw_When_TextIsNotATimestampWithAnOffset(string json)
    {
        // Act
        var act = () => JsonSerializer.Deserialize<DateTimeOffset>(json, Options);

        // Assert
        act.Should().Throw<JsonException>();
    }
}
