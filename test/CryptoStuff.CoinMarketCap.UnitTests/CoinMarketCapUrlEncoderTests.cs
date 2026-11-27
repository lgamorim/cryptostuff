namespace CryptoStuff.CoinMarketCap.UnitTests;

public class CoinMarketCapUrlEncoderTests
{
    [Fact]
    public void Should_JoinValuesWithCommas_When_MultipleValuesProvided()
    {
        var result = CoinMarketCapUrlEncoder.JoinAndEncode(["bitcoin", "ethereum", "litecoin"]);

        result.Should().Be("bitcoin,ethereum,litecoin");
    }

    [Fact]
    public void Should_PercentEncodeReservedCharacters_When_ValueContainsThem()
    {
        var result = CoinMarketCapUrlEncoder.JoinAndEncode(["a,b", "c d", "e&f=g"]);

        result.Should().Be("a%2Cb,c%20d,e%26f%3Dg");
    }

    [Fact]
    public void Should_ReturnSingleEncodedValue_When_OnlyOneValueProvided()
    {
        var result = CoinMarketCapUrlEncoder.JoinAndEncode(["bitcoin"]);

        result.Should().Be("bitcoin");
    }

    [Fact]
    public void Should_ReturnEmptyString_When_NoValuesProvided()
    {
        var result = CoinMarketCapUrlEncoder.JoinAndEncode([]);

        result.Should().BeEmpty();
    }
}
