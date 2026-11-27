using CryptoStuff.CoinMarketCap.UnitTests.TestSupport;
using CryptoStuff.Core;
using Microsoft.Extensions.Time.Testing;

namespace CryptoStuff.CoinMarketCap.UnitTests;

public class CoinMarketCapCryptocurrencyServiceDeveloperDataTests
{
    [Fact]
    public async Task Should_FailUnsupportedNamingCoinMarketCapWithNoCall_When_DeveloperDataIsRequested()
    {
        var client = new FakeCoinMarketCapClient(_ => throw new InvalidOperationException("No call expected."));
        var service = new CoinMarketCapCryptocurrencyService(client, new FakeTimeProvider(), new CoinMarketCapServiceOptions());

        var result = await service.GetDeveloperDataAsync(new CoinDeveloperDataQuery { CoinId = "bitcoin", Date = "01-01-2026" }, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.Unsupported);
        result.ErrorDetail.Should().Contain("CoinMarketCap");
        client.RequestUris.Should().BeEmpty();
    }
}
