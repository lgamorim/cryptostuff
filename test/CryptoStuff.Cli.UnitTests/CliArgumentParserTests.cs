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

    [Fact]
    public void Should_CaptureFeeValueAndRemoveBothTokens_When_FeeOptionGiven()
    {
        var result = CliArgumentParser.Parse(["portfolio", "tx", "add", "1", "buy", "bitcoin", "--fee", "0.5", "2", "100", "2024-01-31T14:30:00Z"]);

        result.Fee.Should().Be("0.5");
        result.Command.Should().Be("portfolio");
        result.Arguments.Should().Equal("tx", "add", "1", "buy", "bitcoin", "2", "100", "2024-01-31T14:30:00Z");
    }

    [Fact]
    public void Should_LeaveFeeOptionPositional_When_NoValueFollowsIt()
    {
        var result = CliArgumentParser.Parse(["portfolio", "tx", "add", "--fee"]);

        result.Fee.Should().BeNull();
        result.Arguments.Should().Equal("tx", "add", "--fee");
    }

    [Fact]
    public void Should_KeepSecondFeeOptionPositional_When_FeeGivenTwice()
    {
        var result = CliArgumentParser.Parse(["portfolio", "tx", "add", "--fee", "1", "--fee", "2"]);

        result.Fee.Should().Be("1");
        result.Arguments.Should().Equal("tx", "add", "--fee", "2");
    }

    [Fact]
    public void Should_TreatJsonAsFlagNotFeeValue_When_JsonFollowsFeeOption()
    {
        var result = CliArgumentParser.Parse(["portfolio", "tx", "add", "--fee", "--json"]);

        result.Fee.Should().BeNull();
        result.JsonOutput.Should().BeTrue();
        result.Arguments.Should().Equal("tx", "add", "--fee");
    }

    [Fact]
    public void Should_KeepNegativeFeeValueVerbatim_When_FeeIsNegative()
    {
        var result = CliArgumentParser.Parse(["portfolio", "tx", "add", "--fee", "-1"]);

        result.Fee.Should().Be("-1");
    }
}
