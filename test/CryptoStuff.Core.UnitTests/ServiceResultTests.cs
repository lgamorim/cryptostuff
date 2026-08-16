namespace CryptoStuff.Core.UnitTests;

public class ServiceResultTests
{
    [Fact]
    public void Should_CarrySuccessValueWithNoErrorCode_When_CreatedViaSuccess()
    {
        var result = ServiceResult<string>.Success("payload");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("payload");
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Should_CarryErrorCodeWithoutValue_When_CreatedViaFailure()
    {
        var result = ServiceResult<string>.Failure(ServiceErrorCode.NotFound);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_CreatedViaSuccessWithNullValue()
    {
        var act = () => ServiceResult<string>.Success(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
