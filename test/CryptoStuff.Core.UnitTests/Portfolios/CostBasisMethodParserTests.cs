using System.Text.Json;
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

    [Theory]
    [InlineData("FIFO", CostBasisMethod.Fifo)]
    [InlineData("Average-Cost", CostBasisMethod.AverageCost)]
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
    [InlineData("lifo")]
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

    // The hosts print an enum with JsonNamingPolicy.KebabCaseLower, so every
    // name printed that way has to parse back to the same method.
    [Fact]
    public void Should_ParseEveryMethodFromItsKebabCaseName_When_NameIsPrintedTheWayHostsPrintIt()
    {
        foreach (var method in Enum.GetValues<CostBasisMethod>())
        {
            var token = JsonNamingPolicy.KebabCaseLower.ConvertName(method.ToString());

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
        var outcome = CostBasisMethodParser.Validate("lifo", "Method");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("Method").And.Contain("average-cost").And.Contain("fifo");
    }
}
