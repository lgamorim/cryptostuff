using System.Text.Json;

namespace CryptoStuff.CoinGecko.UnitTests;

public class CoinGeckoMarketChartPointConverterTests
{
    [Fact]
    public void Should_DeserializeTimestampAndValue_When_ArrayHasTwoNumericElements()
    {
        var point = JsonSerializer.Deserialize<CoinGeckoMarketChartPoint>("[1700000000000,43189.52]");

        point.Should().Be(new CoinGeckoMarketChartPoint(1700000000000, 43189.52m));
    }

    [Fact]
    public void Should_RoundTripThroughSerializeAndDeserialize_When_GivenAPoint()
    {
        var original = new CoinGeckoMarketChartPoint(1700000000000, 43189.52m);

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<CoinGeckoMarketChartPoint>(json);

        roundTripped.Should().Be(original);
    }

    [Fact]
    public void Should_SerializeAsTwoElementJsonArray_When_WritingAPoint()
    {
        var point = new CoinGeckoMarketChartPoint(1700000000000, 43189.52m);

        var json = JsonSerializer.Serialize(point);

        json.Should().Be("[1700000000000,43189.52]");
    }

    [Fact]
    public void Should_ThrowJsonException_When_ArrayHasOnlyOneElement()
    {
        var act = () => JsonSerializer.Deserialize<CoinGeckoMarketChartPoint>("[1700000000000]");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Should_ThrowJsonException_When_ArrayHasThreeElements()
    {
        var act = () => JsonSerializer.Deserialize<CoinGeckoMarketChartPoint>("[1700000000000,43189.52,1]");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Should_ThrowJsonException_When_TimestampElementIsNonNumeric()
    {
        var act = () => JsonSerializer.Deserialize<CoinGeckoMarketChartPoint>("""["not-a-number",43189.52]""");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Should_ThrowJsonException_When_ValueElementIsNonNumeric()
    {
        var act = () => JsonSerializer.Deserialize<CoinGeckoMarketChartPoint>("""[1700000000000,"not-a-number"]""");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Should_ThrowJsonException_When_TokenIsNotAnArray()
    {
        var act = () => JsonSerializer.Deserialize<CoinGeckoMarketChartPoint>("""{"not":"an-array"}""");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Should_ThrowJsonException_When_TokenIsNull()
    {
        var act = () => JsonSerializer.Deserialize<CoinGeckoMarketChartPoint>("null");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Should_ThrowJsonException_When_TimestampElementIsNotRepresentableAsLong()
    {
        var act = () => JsonSerializer.Deserialize<CoinGeckoMarketChartPoint>("[1.5,43189.52]");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Should_ThrowJsonException_When_ValueElementIsNotRepresentableAsDecimal()
    {
        var act = () => JsonSerializer.Deserialize<CoinGeckoMarketChartPoint>("[1700000000000,1e40]");

        act.Should().Throw<JsonException>();
    }
}
