using System.Net;

namespace CryptoStuff.CoinGecko.UnitTests;

public class CoinGeckoResponseTests
{
    [Fact]
    public void Should_CarrySuccessValueAndStatusCode_When_CreatedViaSuccess()
    {
        var response = CoinGeckoResponse<string>.Success("payload", HttpStatusCode.OK);

        response.IsSuccess.Should().BeTrue();
        response.IsTimeout.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Value.Should().Be("payload");
    }

    [Fact]
    public void Should_AllowNullValue_When_CreatedViaSuccessWithNoBody()
    {
        var response = CoinGeckoResponse<string>.Success(null, HttpStatusCode.OK);

        response.IsSuccess.Should().BeTrue();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Value.Should().BeNull();
    }

    [Fact]
    public void Should_CarryFailureStatusCodeWithoutValue_When_CreatedViaFailure()
    {
        var response = CoinGeckoResponse<string>.Failure(HttpStatusCode.NotFound);

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Value.Should().BeNull();
    }

    [Fact]
    public void Should_IndicateTimeoutWithoutStatusCode_When_CreatedViaTimeout()
    {
        var response = CoinGeckoResponse<string>.Timeout();

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeTrue();
        response.StatusCode.Should().BeNull();
        response.Value.Should().BeNull();
    }
}
