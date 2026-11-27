using System.Net;
using CryptoStuff.Core;

namespace CryptoStuff.CoinMarketCap.UnitTests;

public class CoinMarketCapErrorMapperTests
{
    [Fact]
    public void Should_MapToRequestTimedOut_When_ResponseTimedOut()
    {
        var result = CoinMarketCapErrorMapper.Map(CoinMarketCapResponse<object>.Timeout());

        result.Should().Be(ServiceErrorCode.RequestTimedOut);
    }

    [Fact]
    public void Should_MapToRateLimited_When_StatusIsTooManyRequests()
    {
        var result = CoinMarketCapErrorMapper.Map(CoinMarketCapResponse<object>.Failure(HttpStatusCode.TooManyRequests, 1008, "Rate limit reached."));

        result.Should().Be(ServiceErrorCode.RateLimited);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(402)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(500)]
    [InlineData(503)]
    public void Should_MapToUpstreamUnavailable_When_StatusIsAnyOtherFailure(int statusCode)
    {
        var result = CoinMarketCapErrorMapper.Map(CoinMarketCapResponse<object>.Failure((HttpStatusCode)statusCode, null, null));

        result.Should().Be(ServiceErrorCode.UpstreamUnavailable);
    }

    [Fact]
    public void Should_MapToUpstreamUnavailable_When_NoResponseArrived()
    {
        var result = CoinMarketCapErrorMapper.Map(CoinMarketCapResponse<object>.NoResponse());

        result.Should().Be(ServiceErrorCode.UpstreamUnavailable);
    }

    [Fact]
    public void Should_Throw_When_MappingASuccessfulResponse()
    {
        var act = () => CoinMarketCapErrorMapper.Map(CoinMarketCapResponse<object>.Success(new object(), HttpStatusCode.OK));

        act.Should().Throw<ArgumentException>().WithParameterName("response");
    }

    [Fact]
    public void Should_RecognizeRejectedParameter_When_BadRequestMessageNamesIt()
    {
        var response = CoinMarketCapResponse<object>.Failure(HttpStatusCode.BadRequest, 400, "Invalid value for \"convert\": \"XYZ\"");

        var result = CoinMarketCapErrorMapper.RejectsParameter(response, "convert");

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("Invalid value for \"slug\": \"nope\"")]
    [InlineData("Invalid value for \"convert_id\": \"0\"")]
    [InlineData("")]
    [InlineData(null)]
    public void Should_NotRecognizeRejectedParameter_When_BadRequestMessageNamesAnother(string? errorMessage)
    {
        var response = CoinMarketCapResponse<object>.Failure(HttpStatusCode.BadRequest, 400, errorMessage);

        var result = CoinMarketCapErrorMapper.RejectsParameter(response, "convert");

        result.Should().BeFalse();
    }

    [Fact]
    public void Should_NotRecognizeRejectedParameter_When_StatusIsNotBadRequest()
    {
        var response = CoinMarketCapResponse<object>.Failure(HttpStatusCode.Forbidden, 1006, "Invalid value for \"convert\": \"XYZ\"");

        var result = CoinMarketCapErrorMapper.RejectsParameter(response, "convert");

        result.Should().BeFalse();
    }

    [Fact]
    public void Should_NotRecognizeRejectedParameter_When_ResponseSucceeded()
    {
        var response = CoinMarketCapResponse<object>.Success(new object(), HttpStatusCode.OK);

        var result = CoinMarketCapErrorMapper.RejectsParameter(response, "convert");

        result.Should().BeFalse();
    }
}
