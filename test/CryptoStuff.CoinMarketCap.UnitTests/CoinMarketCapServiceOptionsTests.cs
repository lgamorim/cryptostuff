namespace CryptoStuff.CoinMarketCap.UnitTests;

public class CoinMarketCapServiceOptionsTests
{
    [Fact]
    public void Should_QuoteCoinDetailInUsd_When_OptionsAreLeftAtTheirDefault()
    {
        var options = new CoinMarketCapServiceOptions();

        options.DetailCurrencies.Should().Equal("usd");
    }
}
