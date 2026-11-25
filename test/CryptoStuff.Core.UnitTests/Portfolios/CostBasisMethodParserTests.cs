using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Core.UnitTests.Portfolios;

public class CostBasisMethodParserTests
{
    [Fact]
    public void Should_ParseAverageCost_When_TokenIsAverageCost()
    {
        var parsed = CostBasisMethodParser.TryParse("average-cost", out var method);

        parsed.Should().BeTrue();
        method.Should().Be(CostBasisMethod.AverageCost);
    }

    [Fact]
    public void Should_ParseFifo_When_TokenIsFifo()
    {
        var parsed = CostBasisMethodParser.TryParse("fifo", out var method);

        parsed.Should().BeTrue();
        method.Should().Be(CostBasisMethod.Fifo);
    }

    [Fact]
    public void Should_ParseLifo_When_TokenIsLifo()
    {
        var parsed = CostBasisMethodParser.TryParse("lifo", out var method);

        parsed.Should().BeTrue();
        method.Should().Be(CostBasisMethod.Lifo);
    }

    [Fact]
    public void Should_ParseHifo_When_TokenIsHifo()
    {
        var parsed = CostBasisMethodParser.TryParse("hifo", out var method);

        parsed.Should().BeTrue();
        method.Should().Be(CostBasisMethod.Hifo);
    }

    [Theory]
    [InlineData("FIFO", CostBasisMethod.Fifo)]
    [InlineData("Average-Cost", CostBasisMethod.AverageCost)]
    [InlineData("LIFO", CostBasisMethod.Lifo)]
    [InlineData("Hifo", CostBasisMethod.Hifo)]
    public void Should_IgnoreCase_When_TokenDiffersOnlyInCase(string token, CostBasisMethod expected)
    {
        var parsed = CostBasisMethodParser.TryParse(token, out var method);

        parsed.Should().BeTrue();
        method.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("specific-id")]
    [InlineData("average_cost")]
    [InlineData("averagecost")]
    [InlineData("AverageCost")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData(" fifo")]
    [InlineData("fifo ")]
    public void Should_ReturnFalse_When_TokenIsNotAMethod(string? token)
    {
        var parsed = CostBasisMethodParser.TryParse(token, out _);

        parsed.Should().BeFalse();
    }

    // EnumTokens writes the tokens the hosts print and the database stores,
    // so every token it writes has to parse back to the same method.
    [Fact]
    public void Should_ParseEveryMethodFromItsKebabCaseName_When_NameIsPrintedTheWayHostsPrintIt()
    {
        foreach (var method in Enum.GetValues<CostBasisMethod>())
        {
            var token = EnumTokens.ToToken(method);

            CostBasisMethodParser.TryParse(token, out var parsed).Should().BeTrue(token);
            parsed.Should().Be(method);
        }
    }

    [Fact]
    public void Should_ReturnValid_When_ValidateGivenMethodToken()
    {
        var outcome = CostBasisMethodParser.Validate("fifo", "Method");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnInvalidNamingFieldAndAcceptedTokens_When_ValidateGivenUnknownToken()
    {
        var outcome = CostBasisMethodParser.Validate("specific-id", "Method");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Be("Method must be 'average-cost', 'fifo', 'lifo' or 'hifo'.");
    }
}
