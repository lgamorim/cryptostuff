using Microsoft.Extensions.Configuration;

namespace CryptoStuff.Api.UnitTests;

// The integration tests always run with a key supplied through the Api__Key
// environment variable, so only here can the setting be truly absent.
public class ApiKeyAuthenticationTests
{
    private const string MissingKeyMessage =
        "API key is missing. Choose a secret key and set it locally by running "
        + "'dotnet user-secrets set Api:Key <your-api-key>' from the src/CryptoStuff.Api "
        + "project directory, or set the Api__Key environment variable in deployment. "
        + "Clients send it in the X-Api-Key header.";

    [Fact]
    public void Should_ReturnTheConfiguredKey_When_ApiKeyIsSet()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Api:Key"] = "s3cret-Key" })
            .Build();

        var apiKey = ApiKeyAuthentication.ReadApiKey(configuration);

        apiKey.Should().Be("s3cret-Key");
    }

    [Theory]
    [InlineData(" s3cret-Key")]
    [InlineData("s3cret-Key\t")]
    [InlineData("\t s3cret-Key  ")]
    public void Should_ReturnTheKeyWithoutSurroundingWhitespace_When_ApiKeyIsPadded(string apiKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Api:Key"] = apiKey })
            .Build();

        var readKey = ApiKeyAuthentication.ReadApiKey(configuration);

        readKey.Should().Be("s3cret-Key");
    }

    [Fact]
    public void Should_ThrowAnActionableMessage_When_ApiKeyIsAbsent()
    {
        var configuration = new ConfigurationBuilder().Build();

        var act = () => ApiKeyAuthentication.ReadApiKey(configuration);

        act.Should().Throw<InvalidOperationException>().WithMessage(MissingKeyMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Should_ThrowAnActionableMessage_When_ApiKeyIsBlank(string apiKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Api:Key"] = apiKey })
            .Build();

        var act = () => ApiKeyAuthentication.ReadApiKey(configuration);

        act.Should().Throw<InvalidOperationException>().WithMessage(MissingKeyMessage);
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_ConfigurationIsNull()
    {
        // `!` is deliberate: the test hands over the null the guard must reject.
        var act = () => ApiKeyAuthentication.ReadApiKey(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("configuration");
    }
}
