using System.Net;
using CryptoStuff.CoinGecko;
using CryptoStuff.Core.UnitTests.TestSupport;

namespace CryptoStuff.Core.UnitTests;

public class CryptocurrencyServiceTests
{
    [Fact]
    public async Task Should_ReturnMappedCoinPriceView_When_GetPricesAsyncSucceeds()
    {
        var matrix = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["bitcoin"] = new() { ["usd"] = 50000m },
        };
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(matrix, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new CoinPriceQuery { CoinIds = ["bitcoin"], VsCurrencies = ["usd"] };

        var result = await service.GetPricesAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Coins.Should().ContainSingle(c => c.CoinId == "bitcoin");
    }

    [Fact]
    public async Task Should_ReturnMappedFailure_When_GetPricesAsyncResponseIsUnsuccessful()
    {
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Failure(HttpStatusCode.TooManyRequests));
        var service = new CryptocurrencyService(client);
        var query = new CoinPriceQuery { CoinIds = ["bitcoin"], VsCurrencies = ["usd"] };

        var result = await service.GetPricesAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.RateLimited);
    }

    [Fact]
    public async Task Should_ReturnFailureWithNotFound_When_GetPricesAsyncOmitsARequestedCoinId()
    {
        var matrix = new Dictionary<string, Dictionary<string, decimal>>();
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(matrix, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new CoinPriceQuery { CoinIds = ["bitcoin"], VsCurrencies = ["usd"] };

        var result = await service.GetPricesAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnFailureWithNotFound_When_GetPricesAsyncAllRequestedCoinIdsAreMissing()
    {
        var matrix = new Dictionary<string, Dictionary<string, decimal>>();
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(matrix, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new CoinPriceQuery { CoinIds = ["bitcoin", "ethereum"], VsCurrencies = ["usd"] };

        var result = await service.GetPricesAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnFailureWithNotFound_When_GetPricesAsyncSucceedsWithNullMatrixAndCoinIdsWereRequested()
    {
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(null, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new CoinPriceQuery { CoinIds = ["bitcoin"], VsCurrencies = ["usd"] };

        var result = await service.GetPricesAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnSuccessWithEmptyCoinsList_When_GetPricesAsyncCalledWithNoCoinIdsAndNullMatrix()
    {
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(null, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new CoinPriceQuery { CoinIds = [], VsCurrencies = ["usd"] };

        var result = await service.GetPricesAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Coins.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnFailureWithNotFound_When_GetPricesAsyncMatrixKeyCasingDiffersFromRequest()
    {
        var matrix = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["Bitcoin"] = new() { ["usd"] = 50000m },
        };
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(matrix, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new CoinPriceQuery { CoinIds = ["bitcoin"], VsCurrencies = ["usd"] };

        var result = await service.GetPricesAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_ForwardCancellationToken_When_GetPricesAsyncCalled()
    {
        using var cts = new CancellationTokenSource();
        var matrix = new Dictionary<string, Dictionary<string, decimal>> { ["bitcoin"] = new() { ["usd"] = 1m } };
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(matrix, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new CoinPriceQuery { CoinIds = ["bitcoin"], VsCurrencies = ["usd"] };

        await service.GetPricesAsync(query, cts.Token);

        client.CapturedCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Should_ReturnMappedTokenPriceView_When_GetTokenPricesAsyncSucceeds()
    {
        var matrix = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["0xaaa"] = new() { ["usd"] = 1m },
        };
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(matrix, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new TokenPriceQuery { Platform = "ethereum", ContractAddresses = ["0xaaa"], VsCurrencies = ["usd"] };

        var result = await service.GetTokenPricesAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Tokens.Should().ContainSingle(t => t.ContractAddress == "0xaaa");
    }

    [Fact]
    public async Task Should_ReturnMappedFailure_When_GetTokenPricesAsyncResponseIsUnsuccessful()
    {
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Failure(HttpStatusCode.NotFound));
        var service = new CryptocurrencyService(client);
        var query = new TokenPriceQuery { Platform = "ethereum", ContractAddresses = ["0xaaa"], VsCurrencies = ["usd"] };

        var result = await service.GetTokenPricesAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnFailureWithNotFound_When_GetTokenPricesAsyncOmitsARequestedAddress()
    {
        var matrix = new Dictionary<string, Dictionary<string, decimal>>();
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(matrix, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new TokenPriceQuery { Platform = "ethereum", ContractAddresses = ["0xaaa"], VsCurrencies = ["usd"] };

        var result = await service.GetTokenPricesAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnFailureWithNotFound_When_GetTokenPricesAsyncSucceedsWithNullMatrixAndAddressesWereRequested()
    {
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(null, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new TokenPriceQuery { Platform = "ethereum", ContractAddresses = ["0xaaa"], VsCurrencies = ["usd"] };

        var result = await service.GetTokenPricesAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnSuccessWithEmptyTokensList_When_GetTokenPricesAsyncCalledWithNoAddressesAndNullMatrix()
    {
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(null, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new TokenPriceQuery { Platform = "ethereum", ContractAddresses = [], VsCurrencies = ["usd"] };

        var result = await service.GetTokenPricesAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Tokens.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnFailureWithNotFound_When_GetTokenPricesAsyncMatrixKeyCasingDiffersFromRequest()
    {
        var matrix = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["0xAAA"] = new() { ["usd"] = 1m },
        };
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(matrix, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new TokenPriceQuery { Platform = "ethereum", ContractAddresses = ["0xaaa"], VsCurrencies = ["usd"] };

        var result = await service.GetTokenPricesAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_ForwardCancellationToken_When_GetTokenPricesAsyncCalled()
    {
        using var cts = new CancellationTokenSource();
        var matrix = new Dictionary<string, Dictionary<string, decimal>> { ["0xaaa"] = new() { ["usd"] = 1m } };
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(matrix, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new TokenPriceQuery { Platform = "ethereum", ContractAddresses = ["0xaaa"], VsCurrencies = ["usd"] };

        await service.GetTokenPricesAsync(query, cts.Token);

        client.CapturedCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Should_ReturnMappedCoinMarketChartView_When_GetMarketChartAsyncSucceeds()
    {
        var chart = new CoinGeckoMarketChart
        {
            Prices = [new CoinGeckoMarketChartPoint(1704067200000, 1m)],
            MarketCaps = [],
            TotalVolumes = [],
        };
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<CoinGeckoMarketChart>.Success(chart, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new CoinMarketChartQuery { CoinId = "bitcoin", VsCurrency = "usd", Days = 1 };

        var result = await service.GetMarketChartAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Prices.Should().ContainSingle();
    }

    [Fact]
    public async Task Should_ReturnMappedFailure_When_GetMarketChartAsyncResponseIsUnsuccessful()
    {
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<CoinGeckoMarketChart>.Timeout());
        var service = new CryptocurrencyService(client);
        var query = new CoinMarketChartQuery { CoinId = "bitcoin", VsCurrency = "usd", Days = 1 };

        var result = await service.GetMarketChartAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.RequestTimedOut);
    }

    [Fact]
    public async Task Should_ReturnFailureWithNotFound_When_GetMarketChartAsyncSucceedsWithNullBody()
    {
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<CoinGeckoMarketChart>.Success(null, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new CoinMarketChartQuery { CoinId = "bitcoin", VsCurrency = "usd", Days = 1 };

        var result = await service.GetMarketChartAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_ForwardCancellationToken_When_GetMarketChartAsyncCalled()
    {
        using var cts = new CancellationTokenSource();
        var chart = new CoinGeckoMarketChart { Prices = [], MarketCaps = [], TotalVolumes = [] };
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<CoinGeckoMarketChart>.Success(chart, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new CoinMarketChartQuery { CoinId = "bitcoin", VsCurrency = "usd", Days = 1 };

        await service.GetMarketChartAsync(query, cts.Token);

        client.CapturedCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Should_ReturnMappedCoinView_When_GetCoinAsyncSucceeds()
    {
        var coin = new CoinGeckoCoin { Id = "bitcoin", Symbol = "btc", Name = "Bitcoin", Image = new CoinGeckoImage() };
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<CoinGeckoCoin>.Success(coin, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new CoinQuery { CoinId = "bitcoin" };

        var result = await service.GetCoinAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be("bitcoin");
    }

    [Fact]
    public async Task Should_ReturnMappedFailure_When_GetCoinAsyncResponseIsUnsuccessful()
    {
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<CoinGeckoCoin>.Failure(HttpStatusCode.InternalServerError));
        var service = new CryptocurrencyService(client);
        var query = new CoinQuery { CoinId = "bitcoin" };

        var result = await service.GetCoinAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.UpstreamUnavailable);
    }

    [Fact]
    public async Task Should_ReturnFailureWithNotFound_When_GetCoinAsyncSucceedsWithNullBody()
    {
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<CoinGeckoCoin>.Success(null, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new CoinQuery { CoinId = "bitcoin" };

        var result = await service.GetCoinAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_ForwardCancellationToken_When_GetCoinAsyncCalled()
    {
        using var cts = new CancellationTokenSource();
        var coin = new CoinGeckoCoin { Id = "bitcoin", Symbol = "btc", Name = "Bitcoin", Image = new CoinGeckoImage() };
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<CoinGeckoCoin>.Success(coin, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new CoinQuery { CoinId = "bitcoin" };

        await service.GetCoinAsync(query, cts.Token);

        client.CapturedCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Should_ReturnMappedCoinDeveloperDataView_When_GetDeveloperDataAsyncSucceeds()
    {
        var history = new CoinGeckoCoinHistory { Id = "bitcoin", Symbol = "btc", Name = "Bitcoin" };
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<CoinGeckoCoinHistory>.Success(history, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new CoinDeveloperDataQuery { CoinId = "bitcoin", Date = "01-01-2024" };

        var result = await service.GetDeveloperDataAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be("bitcoin");
    }

    [Fact]
    public async Task Should_ReturnMappedFailure_When_GetDeveloperDataAsyncResponseIsUnsuccessful()
    {
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<CoinGeckoCoinHistory>.Failure(HttpStatusCode.NotFound));
        var service = new CryptocurrencyService(client);
        var query = new CoinDeveloperDataQuery { CoinId = "bitcoin", Date = "01-01-2024" };

        var result = await service.GetDeveloperDataAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnFailureWithNotFound_When_GetDeveloperDataAsyncSucceedsWithNullBody()
    {
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<CoinGeckoCoinHistory>.Success(null, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new CoinDeveloperDataQuery { CoinId = "bitcoin", Date = "01-01-2024" };

        var result = await service.GetDeveloperDataAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_PassDateVerbatimToClient_When_GetDeveloperDataAsyncCalled()
    {
        var history = new CoinGeckoCoinHistory { Id = "bitcoin", Symbol = "btc", Name = "Bitcoin" };
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<CoinGeckoCoinHistory>.Success(history, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new CoinDeveloperDataQuery { CoinId = "bitcoin", Date = "01-01-2024" };

        await service.GetDeveloperDataAsync(query, CancellationToken.None);

        client.CapturedRequestUri.Should().Contain("date=01-01-2024");
    }

    [Fact]
    public async Task Should_ForwardCancellationToken_When_GetDeveloperDataAsyncCalled()
    {
        using var cts = new CancellationTokenSource();
        var history = new CoinGeckoCoinHistory { Id = "bitcoin", Symbol = "btc", Name = "Bitcoin" };
        var client = new FakeCoinGeckoClient(CoinGeckoResponse<CoinGeckoCoinHistory>.Success(history, HttpStatusCode.OK));
        var service = new CryptocurrencyService(client);
        var query = new CoinDeveloperDataQuery { CoinId = "bitcoin", Date = "01-01-2024" };

        await service.GetDeveloperDataAsync(query, cts.Token);

        client.CapturedCancellationToken.Should().Be(cts.Token);
    }
}
