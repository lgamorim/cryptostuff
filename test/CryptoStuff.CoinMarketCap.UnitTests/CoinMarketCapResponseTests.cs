using System.Net;

namespace CryptoStuff.CoinMarketCap.UnitTests;

public class CoinMarketCapResponseTests
{
    [Fact]
    public void Should_CarrySuccessValueAndStatusCode_When_CreatedViaSuccess()
    {
        var response = CoinMarketCapResponse<string>.Success("payload", HttpStatusCode.OK);

        response.IsSuccess.Should().BeTrue();
        response.IsTimeout.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Value.Should().Be("payload");
        response.ErrorCode.Should().BeNull();
        response.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Should_AllowNullValue_When_CreatedViaSuccessWithNoData()
    {
        var response = CoinMarketCapResponse<string>.Success(null, HttpStatusCode.OK);

        response.IsSuccess.Should().BeTrue();
        response.Value.Should().BeNull();
    }

    [Fact]
    public void Should_CarryStatusCodeAndCoinMarketCapError_When_CreatedViaFailure()
    {
        var response = CoinMarketCapResponse<string>.Failure(HttpStatusCode.BadRequest, 400, "Invalid value for \"slug\"");

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Value.Should().BeNull();
        response.ErrorCode.Should().Be(400);
        response.ErrorMessage.Should().Be("Invalid value for \"slug\"");
    }

    [Fact]
    public void Should_CarryStatusCodeWithoutCoinMarketCapError_When_CreatedViaFailureWithNoErrorBody()
    {
        var response = CoinMarketCapResponse<string>.Failure(HttpStatusCode.BadGateway, null, null);

        response.IsSuccess.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        response.ErrorCode.Should().BeNull();
        response.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Should_IndicateTimeoutWithoutStatusCode_When_CreatedViaTimeout()
    {
        var response = CoinMarketCapResponse<string>.Timeout();

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeTrue();
        response.StatusCode.Should().BeNull();
        response.Value.Should().BeNull();
        response.ErrorCode.Should().BeNull();
        response.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Should_IndicateFailureWithoutStatusCodeOrTimeout_When_CreatedViaNoResponse()
    {
        var response = CoinMarketCapResponse<string>.NoResponse();

        response.IsSuccess.Should().BeFalse();
        response.IsTimeout.Should().BeFalse();
        response.StatusCode.Should().BeNull();
        response.Value.Should().BeNull();
        response.ErrorCode.Should().BeNull();
        response.ErrorMessage.Should().BeNull();
    }
}
