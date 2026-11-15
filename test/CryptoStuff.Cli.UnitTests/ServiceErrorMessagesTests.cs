using CryptoStuff.Core;

namespace CryptoStuff.Cli.UnitTests;

public class ServiceErrorMessagesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ReturnTheCodesOwnMessage_When_DetailIsMissingOrBlank(string? detail)
    {
        var message = ServiceErrorMessages.For(ServiceErrorCode.NotFound, detail);

        message.Should().Be("Error: the requested resource was not found.");
    }

    [Fact]
    public void Should_ReturnTheDetail_When_DetailIsPresent()
    {
        var message = ServiceErrorMessages.For(ServiceErrorCode.NotFound, "Watchlist 7 was not found.");

        message.Should().Be("Error: Watchlist 7 was not found.");
    }
}
