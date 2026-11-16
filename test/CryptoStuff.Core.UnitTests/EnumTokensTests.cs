using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Core.UnitTests;

// That the hosts' JSON carries these same tokens is pinned against the real
// configuration, in Composition's CryptoStuffJsonTests.
public class EnumTokensTests
{
    [Theory]
    [InlineData(CostBasisMethod.AverageCost, "average-cost")]
    [InlineData(CostBasisMethod.Fifo, "fifo")]
    public void Should_WriteTheLowercaseKebabCaseName_When_MethodTokenized(CostBasisMethod method, string token)
    {
        var written = EnumTokens.ToToken(method);

        written.Should().Be(token);
    }
}
