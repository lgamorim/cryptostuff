namespace CryptoStuff.Cli.UnitTests;

public class CliArgumentParserTests
{
    [Fact]
    public void Should_ReturnNullCommandAndNoArguments_When_ArgsEmpty()
    {
        var result = CliArgumentParser.Parse([]);

        result.Command.Should().BeNull();
        result.Arguments.Should().BeEmpty();
        result.JsonOutput.Should().BeFalse();
    }

    [Fact]
    public void Should_ParseCommandWithNoArguments_When_OnlyCommandGiven()
    {
        var result = CliArgumentParser.Parse(["coin"]);

        result.Command.Should().Be("coin");
        result.Arguments.Should().BeEmpty();
    }

    [Fact]
    public void Should_ParseCommandAndArguments_When_MultipleArgumentsGiven()
    {
        var result = CliArgumentParser.Parse(["price", "bitcoin", "usd"]);

        result.Command.Should().Be("price");
        result.Arguments.Should().Equal("bitcoin", "usd");
    }

    [Fact]
    public void Should_SetJsonOutput_When_JsonFlagTrailing()
    {
        var result = CliArgumentParser.Parse(["price", "bitcoin", "usd", "--json"]);

        result.JsonOutput.Should().BeTrue();
        result.Command.Should().Be("price");
        result.Arguments.Should().Equal("bitcoin", "usd");
    }

    [Fact]
    public void Should_SetJsonOutput_When_JsonFlagLeading()
    {
        var result = CliArgumentParser.Parse(["--json", "price", "bitcoin", "usd"]);

        result.JsonOutput.Should().BeTrue();
        result.Command.Should().Be("price");
        result.Arguments.Should().Equal("bitcoin", "usd");
    }

    [Fact]
    public void Should_SetJsonOutput_When_JsonFlagInterspersed()
    {
        var result = CliArgumentParser.Parse(["price", "--json", "bitcoin", "usd"]);

        result.JsonOutput.Should().BeTrue();
        result.Command.Should().Be("price");
        result.Arguments.Should().Equal("bitcoin", "usd");
    }

    [Fact]
    public void Should_LeaveJsonOutputFalse_When_JsonFlagAbsent()
    {
        var result = CliArgumentParser.Parse(["coin", "bitcoin"]);

        result.JsonOutput.Should().BeFalse();
    }
}
