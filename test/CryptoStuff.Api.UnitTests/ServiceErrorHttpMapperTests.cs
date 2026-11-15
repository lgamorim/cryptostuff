using CryptoStuff.Core;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CryptoStuff.Api.UnitTests;

public class ServiceErrorHttpMapperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_UseTheCodesOwnDetail_When_DetailIsMissingOrBlank(string? detail)
    {
        var result = ServiceErrorHttpMapper.ToProblem(ServiceErrorCode.NotFound, detail);

        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject.ProblemDetails;
        problem.Status.Should().Be(404);
        problem.Detail.Should().Be("The requested resource was not found.");
    }

    [Fact]
    public void Should_UseTheDetail_When_DetailIsPresent()
    {
        var result = ServiceErrorHttpMapper.ToProblem(ServiceErrorCode.NotFound, "Watchlist 7 was not found.");

        result.Should().BeOfType<ProblemHttpResult>().Subject.ProblemDetails.Detail.Should().Be("Watchlist 7 was not found.");
    }
}
