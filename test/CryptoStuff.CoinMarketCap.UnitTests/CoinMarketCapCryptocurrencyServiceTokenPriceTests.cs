using System.Net;
using CryptoStuff.CoinMarketCap.UnitTests.TestSupport;
using CryptoStuff.Core;
using Microsoft.Extensions.Time.Testing;
using static CryptoStuff.CoinMarketCap.UnitTests.TestSupport.CoinMarketCapScript;

namespace CryptoStuff.CoinMarketCap.UnitTests;

public class CoinMarketCapCryptocurrencyServiceTokenPriceTests
{
    private const string UsdcAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
    private const string UsdtAddress = "0xdac17f958d2ee523a2206206994597c13d831ec7";

    private static CoinMarketCapCryptocurrencyService CreateService(ICoinMarketCapClient client) =>
        new(client, new FakeTimeProvider(), new CoinMarketCapServiceOptions());

    private static TokenPriceQuery Query(string platform, string[] addresses, string[] currencies) =>
        new() { Platform = platform, ContractAddresses = addresses, VsCurrencies = currencies };

    private static TokenPriceView ValueOf(ServiceResult<TokenPriceView> result)
    {
        result.IsSuccess.Should().BeTrue();
        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        return result.Value!;
    }

    /// <summary>
    /// A script answering <c>info</c> by address from <paramref name="infoByAddress"/>
    /// and <c>quotes/latest</c> with <paramref name="latest"/>.
    /// </summary>
    private static Func<string, object> Script(IReadOnlyDictionary<string, object> infoByAddress, object? latest = null) =>
        requestUri => IsInfo(requestUri)
            ? infoByAddress[Uri.UnescapeDataString(QueryValue(requestUri, "address"))]
            : latest ?? throw new InvalidOperationException("No quotes/latest call expected.");

    private static Dictionary<string, object> TwoTokens() => new()
    {
        [UsdcAddress] = Info(InfoCoin("usd-coin", 3408, [Contract(UsdcAddress.ToLowerInvariant(), "ethereum")])),
        [UsdtAddress] = Info(InfoCoin("tether", 825, [Contract(UsdtAddress, "bnb"), Contract(UsdtAddress, "ethereum")])),
    };

    [Fact]
    public async Task Should_PriceEachAddressKeyedAsRequestedInQueryOrder_When_EveryAddressResolves()
    {
        var client = new FakeCoinMarketCapClient(Script(TwoTokens(), Latest(Coin("tether", 825, Quote("USD", 1.001m)), Coin("usd-coin", 3408, Quote("USD", 0.999m)))));

        var result = await CreateService(client).GetTokenPricesAsync(Query("ethereum", [UsdcAddress, UsdtAddress], ["usd"]), CancellationToken.None);

        ValueOf(result).Tokens.Should().BeEquivalentTo(
            [
                new TokenPrice { ContractAddress = UsdcAddress, Amounts = [new CurrencyAmount { Currency = "usd", Amount = 0.999m }] },
                new TokenPrice { ContractAddress = UsdtAddress, Amounts = [new CurrencyAmount { Currency = "usd", Amount = 1.001m }] },
            ],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Should_ResolveEachAddressThroughInfoThenPriceTheCoinsTogether_When_PricingTokens()
    {
        var client = new FakeCoinMarketCapClient(Script(TwoTokens(), Latest(Coin("usd-coin", 3408, Quote("USD", 1m)), Coin("tether", 825, Quote("USD", 1m)))));

        await CreateService(client).GetTokenPricesAsync(Query("ethereum", [UsdcAddress, UsdtAddress], ["usd"]), CancellationToken.None);

        client.RequestUris.Should().Equal(
            $"v2/cryptocurrency/info?address={UsdcAddress}",
            $"v2/cryptocurrency/info?address={UsdtAddress}",
            "v3/cryptocurrency/quotes/latest?slug=usd-coin,tether&convert=USD&skip_invalid=true");
    }

    [Fact]
    public async Task Should_AskForTheCoinOnce_When_TwoRequestedAddressesResolveToIt()
    {
        var lower = UsdcAddress.ToLowerInvariant();
        var client = new FakeCoinMarketCapClient(Script(
            new Dictionary<string, object>
            {
                [UsdcAddress] = Info(InfoCoin("usd-coin", 3408, [Contract(lower, "ethereum")])),
                [lower] = Info(InfoCoin("usd-coin", 3408, [Contract(lower, "ethereum")])),
            },
            Latest(Coin("usd-coin", 3408, Quote("USD", 1m)))));

        var result = await CreateService(client).GetTokenPricesAsync(Query("ethereum", [UsdcAddress, lower], ["usd"]), CancellationToken.None);

        ValueOf(result).Tokens.Select(token => token.ContractAddress).Should().Equal(UsdcAddress, lower);
        client.RequestUris.Last().Should().Be("v3/cryptocurrency/quotes/latest?slug=usd-coin&convert=USD&skip_invalid=true");
    }

    [Fact]
    public async Task Should_ResolveARepeatedAddressOnce_When_QueryRepeatsIt()
    {
        var client = new FakeCoinMarketCapClient(Script(TwoTokens(), Latest(Coin("usd-coin", 3408, Quote("USD", 1m)))));

        var result = await CreateService(client).GetTokenPricesAsync(Query("ethereum", [UsdcAddress, UsdcAddress], ["usd"]), CancellationToken.None);

        ValueOf(result).Tokens.Should().HaveCount(2);
        client.RequestUris.Count(IsInfo).Should().Be(1);
    }

    [Theory]
    [InlineData("ethereum")]
    [InlineData("fantom")]
    public async Task Should_ReturnNoTokensAndMakeNoCall_When_QueryNamesNoAddresses(string platform)
    {
        var client = new FakeCoinMarketCapClient(_ => throw new InvalidOperationException("No call expected."));

        var result = await CreateService(client).GetTokenPricesAsync(Query(platform, [], ["usd"]), CancellationToken.None);

        ValueOf(result).Tokens.Should().BeEmpty();
        client.RequestUris.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnNotFoundAndMakeNoCall_When_ThePlatformIsUnlisted()
    {
        var client = new FakeCoinMarketCapClient(_ => throw new InvalidOperationException("No call expected."));

        var result = await CreateService(client).GetTokenPricesAsync(Query("fantom", [UsdcAddress], ["usd"]), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        client.RequestUris.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnNotFoundAndStopCalling_When_CoinMarketCapRejectsAnAddress()
    {
        var client = new FakeCoinMarketCapClient(Script(new Dictionary<string, object>
        {
            [UsdcAddress] = InfoFailure(HttpStatusCode.BadRequest, $"Invalid value for \"address\": \"{UsdcAddress}\""),
        }));

        var result = await CreateService(client).GetTokenPricesAsync(Query("ethereum", [UsdcAddress, UsdtAddress], ["usd"]), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        client.RequestUris.Should().ContainSingle();
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_TheResolvedCoinHoldsTheAddressOnlyOnAnotherPlatform()
    {
        var client = new FakeCoinMarketCapClient(Script(new Dictionary<string, object>
        {
            [UsdtAddress] = Info(InfoCoin("tether", 825, [Contract(UsdtAddress, "bnb")])),
        }));

        var result = await CreateService(client).GetTokenPricesAsync(Query("ethereum", [UsdtAddress], ["usd"]), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_ResolveToTheCoinListingTheAddressOnThePlatform_When_TheInfoAnswerHoldsSeveralCoins()
    {
        var client = new FakeCoinMarketCapClient(Script(
            new Dictionary<string, object>
            {
                [UsdtAddress] = Info(InfoCoin("bridged-tether", 1, [Contract(UsdtAddress, "bnb")]), InfoCoin("tether", 825, [Contract(UsdtAddress, "ethereum")])),
            },
            Latest(Coin("tether", 825, Quote("USD", 1.001m)))));

        var result = await CreateService(client).GetTokenPricesAsync(Query("ethereum", [UsdtAddress], ["usd"]), CancellationToken.None);

        ValueOf(result).Tokens.Should().ContainSingle()
            .Which.Amounts.Should().Equal(new CurrencyAmount { Currency = "usd", Amount = 1.001m });
        client.RequestUris.Should().EndWith("v3/cryptocurrency/quotes/latest?slug=tether&convert=USD&skip_invalid=true");
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_TheResolvedCoinsContractListLacksTheAddress()
    {
        var client = new FakeCoinMarketCapClient(Script(new Dictionary<string, object>
        {
            [UsdtAddress] = Info(InfoCoin("tether", 825, [Contract(UsdcAddress, "ethereum")])),
        }));

        var result = await CreateService(client).GetTokenPricesAsync(Query("ethereum", [UsdtAddress], ["usd"]), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_TheInfoAnswerHoldsNoCoin()
    {
        var client = new FakeCoinMarketCapClient(Script(new Dictionary<string, object> { [UsdtAddress] = Info() }));

        var result = await CreateService(client).GetTokenPricesAsync(Query("ethereum", [UsdtAddress], ["usd"]), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, null, ServiceErrorCode.RateLimited)]
    [InlineData(HttpStatusCode.Unauthorized, null, ServiceErrorCode.UpstreamUnavailable)]
    [InlineData(HttpStatusCode.BadRequest, "Invalid value for \"aux\": \"x\"", ServiceErrorCode.UpstreamUnavailable)]
    public async Task Should_FailWithTheMappedCode_When_AnInfoCallFails(HttpStatusCode statusCode, string? errorMessage, ServiceErrorCode expected)
    {
        var client = new FakeCoinMarketCapClient(Script(new Dictionary<string, object> { [UsdtAddress] = InfoFailure(statusCode, errorMessage) }));

        var result = await CreateService(client).GetTokenPricesAsync(Query("ethereum", [UsdtAddress], ["usd"]), CancellationToken.None);

        result.ErrorCode.Should().Be(expected);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_TheResolvedCoinIsMissingFromTheQuotes()
    {
        var client = new FakeCoinMarketCapClient(Script(TwoTokens(), Latest()));

        var result = await CreateService(client).GetTokenPricesAsync(Query("ethereum", [UsdtAddress], ["usd"]), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_LeaveTokensWithoutAnAmountInACurrency_When_CoinMarketCapRejectsThatCurrency()
    {
        var client = new FakeCoinMarketCapClient(requestUri =>
            IsInfo(requestUri) ? TwoTokens()[UsdtAddress]
            : QueryValue(requestUri, "convert") == "XYZ" ? RejectedCurrency("XYZ")
            : Latest(Coin("tether", 825, Quote("USD", 1m))));

        var result = await CreateService(client).GetTokenPricesAsync(Query("ethereum", [UsdtAddress], ["xyz", "usd"]), CancellationToken.None);

        ValueOf(result).Tokens.Should().ContainSingle()
            .Which.Amounts.Should().Equal(new CurrencyAmount { Currency = "usd", Amount = 1m });
    }

    [Fact]
    public async Task Should_GiveEachCurrencyOneAmount_When_QueryRepeatsItInAnotherCase()
    {
        var client = new FakeCoinMarketCapClient(Script(TwoTokens(), Latest(Coin("tether", 825, Quote("USD", 1m)))));

        var result = await CreateService(client).GetTokenPricesAsync(Query("ethereum", [UsdtAddress], ["usd", "USD"]), CancellationToken.None);

        ValueOf(result).Tokens.Should().ContainSingle()
            .Which.Amounts.Should().Equal(new CurrencyAmount { Currency = "usd", Amount = 1m });
    }

    [Fact]
    public async Task Should_PassTheCallersCancellationToken_When_Calling()
    {
        using var cancellationSource = new CancellationTokenSource();
        var client = new FakeCoinMarketCapClient(Script(TwoTokens(), Latest(Coin("usd-coin", 3408, Quote("USD", 1m)), Coin("tether", 825, Quote("USD", 1m)))));

        await CreateService(client).GetTokenPricesAsync(Query("ethereum", [UsdcAddress, UsdtAddress], ["usd"]), cancellationSource.Token);

        client.CancellationTokens.Should().Equal(cancellationSource.Token, cancellationSource.Token, cancellationSource.Token);
    }

    // Upstream JSON: `required` rejects only a missing field, so an explicit
    // null still reaches the service at each of these points.

    public static TheoryData<string> NullPoints => new()
    {
        "info data", "coin", "coin slug", "contract list", "contract", "contract address", "platform", "platform coin", "platform slug",
    };

    [Theory]
    [MemberData(nameof(NullPoints))]
    public async Task Should_ReturnNotFound_When_TheInfoAnswerIsNullAt(string point)
    {
        var client = new FakeCoinMarketCapClient(Script(new Dictionary<string, object> { [UsdtAddress] = InfoNullAt(point) }));

        var result = await CreateService(client).GetTokenPricesAsync(Query("ethereum", [UsdtAddress], ["usd"]), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    // `!` is deliberate throughout: each case hands over the null upstream JSON can carry.
    private static CoinMarketCapResponse<IReadOnlyDictionary<string, CoinMarketCapCoinInfo>> InfoNullAt(string point) => point switch
    {
        "info data" => CoinMarketCapResponse<IReadOnlyDictionary<string, CoinMarketCapCoinInfo>>.Success(null, HttpStatusCode.OK),
        "coin" => Info([null!]),
        "coin slug" => Info(InfoCoin(null!, 825, [Contract(UsdtAddress, "ethereum")])),
        "contract list" => Info(InfoCoin("tether", 825, null)),
        "contract" => Info(InfoCoin("tether", 825, [null!])),
        "contract address" => Info(InfoCoin("tether", 825, [Contract(null!, "ethereum")])),
        "platform" => Info(InfoCoin("tether", 825, [new CoinMarketCapContractAddress { Address = UsdtAddress, Platform = null! }])),
        "platform coin" => Info(InfoCoin("tether", 825, [new CoinMarketCapContractAddress { Address = UsdtAddress, Platform = new CoinMarketCapContractPlatform { Coin = null! } }])),
        "platform slug" => Info(InfoCoin("tether", 825, [Contract(UsdtAddress, null!)])),
        _ => throw new ArgumentOutOfRangeException(nameof(point), point, "Not a null point the test knows."),
    };
}
