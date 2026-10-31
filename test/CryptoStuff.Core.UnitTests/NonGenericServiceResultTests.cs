namespace CryptoStuff.Core.UnitTests;

public class NonGenericServiceResultTests
{
    [Fact]
    public void Should_BeSuccessfulWithNoErrorCodeOrDetail_When_CreatedViaSuccess()
    {
        var result = ServiceResult.Success();

        result.IsSuccess.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
        result.ErrorDetail.Should().BeNull();
    }

    [Fact]
    public void Should_CarryErrorCodeWithoutDetail_When_CreatedViaFailureWithoutDetail()
    {
        var result = ServiceResult.Failure(ServiceErrorCode.NotFound);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        result.ErrorDetail.Should().BeNull();
    }

    [Fact]
    public void Should_CarryErrorCodeAndDetail_When_CreatedViaFailureWithDetail()
    {
        var result = ServiceResult.Failure(ServiceErrorCode.PreconditionFailed, "The version is stale.");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.PreconditionFailed);
        result.ErrorDetail.Should().Be("The version is stale.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Should_NormalizeErrorDetailToNull_When_DetailIsNullOrWhitespace(string? detail)
    {
        var result = ServiceResult.Failure(ServiceErrorCode.Conflict, detail);

        result.ErrorDetail.Should().BeNull();
    }
}
