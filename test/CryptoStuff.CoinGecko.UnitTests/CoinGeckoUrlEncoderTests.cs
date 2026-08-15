namespace CryptoStuff.CoinGecko.UnitTests;

public class CoinGeckoUrlEncoderTests
{
    [Fact]
    public void Should_JoinValuesWithCommas_When_MultipleValuesProvided()
    {
        var result = CoinGeckoUrlEncoder.JoinAndEncode(["bitcoin", "ethereum", "litecoin"]);

        result.Should().Be("bitcoin,ethereum,litecoin");
    }

    [Fact]
    public void Should_PercentEncodeReservedCharacters_When_ValueContainsThem()
    {
        var result = CoinGeckoUrlEncoder.JoinAndEncode(["a,b", "c d"]);

        result.Should().Be("a%2Cb,c%20d");
    }

    [Fact]
    public void Should_ReturnSingleEncodedValue_When_OnlyOneValueProvided()
    {
        var result = CoinGeckoUrlEncoder.JoinAndEncode(["bitcoin"]);

        result.Should().Be("bitcoin");
    }

    [Fact]
    public void Should_ReturnEmptyString_When_NoValuesProvided()
    {
        var result = CoinGeckoUrlEncoder.JoinAndEncode([]);

        result.Should().BeEmpty();
    }
}
