using System.Net;
using CryptoStuff.CoinGecko;

namespace CryptoStuff.Core.UnitTests;

public class ServiceErrorCodeMapperTests
{
    [Fact]
    public void Should_MapToNotFound_When_StatusCodeIsNotFound()
    {
        var response = CoinGeckoResponse<string>.Failure(HttpStatusCode.NotFound);

        var errorCode = ServiceErrorCodeMapper.Map(response);

        errorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public void Should_MapToRateLimited_When_StatusCodeIsTooManyRequests()
    {
        var response = CoinGeckoResponse<string>.Failure(HttpStatusCode.TooManyRequests);

        var errorCode = ServiceErrorCodeMapper.Map(response);

        errorCode.Should().Be(ServiceErrorCode.RateLimited);
    }

    [Fact]
    public void Should_MapToRequestTimedOut_When_ResponseTimedOut()
    {
        var response = CoinGeckoResponse<string>.Timeout();

        var errorCode = ServiceErrorCodeMapper.Map(response);

        errorCode.Should().Be(ServiceErrorCode.RequestTimedOut);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadGateway)]
    public void Should_MapToUpstreamUnavailable_When_StatusCodeIsAnythingElse(HttpStatusCode statusCode)
    {
        var response = CoinGeckoResponse<string>.Failure(statusCode);

        var errorCode = ServiceErrorCodeMapper.Map(response);

        errorCode.Should().Be(ServiceErrorCode.UpstreamUnavailable);
    }

    [Fact]
    public void Should_ThrowArgumentException_When_ResponseIsSuccess()
    {
        var response = CoinGeckoResponse<string>.Success("payload", HttpStatusCode.OK);

        var act = () => ServiceErrorCodeMapper.Map(response);

        act.Should().Throw<ArgumentException>();
    }
}
