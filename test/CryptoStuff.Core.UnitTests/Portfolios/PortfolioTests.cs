using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Core.UnitTests.Portfolios;

public class PortfolioTests
{
    // Create.

    [Fact]
    public void Should_HaveGivenNameCurrencyAndMethodAndNoTransactions_When_Created()
    {
        var portfolio = Portfolio.Create("Long term", "usd", CostBasisMethod.Fifo);

        portfolio.Name.Should().Be("Long term");
        portfolio.VsCurrency.Should().Be("usd");
        portfolio.CostBasisMethod.Should().Be(CostBasisMethod.Fifo);
        portfolio.Transactions.Should().BeEmpty();
    }

    [Fact]
    public void Should_HaveUnassignedId_When_Created()
    {
        var portfolio = Portfolio.Create("Long term", "usd", CostBasisMethod.Fifo);

        portfolio.Id.Should().Be(0);
    }

    [Fact]
    public void Should_StartAtVersionOne_When_Created()
    {
        var portfolio = Portfolio.Create("Long term", "usd", CostBasisMethod.Fifo);

        portfolio.Version.Should().Be(1);
    }

    [Fact]
    public void Should_StoreLowercasedCurrency_When_CreatedWithUppercaseCurrency()
    {
        var portfolio = Portfolio.Create("Long term", "USD", CostBasisMethod.Fifo);

        portfolio.VsCurrency.Should().Be("usd");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowArgumentException_When_CreatedWithBlankName(string? name)
    {
        // `!` lets the theory pass null past the non-nullable parameter to exercise the guard.
        var act = () => Portfolio.Create(name!, "usd", CostBasisMethod.Fifo);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_ThrowArgumentException_When_CreatedWithNameLongerThanMaxLength()
    {
        var act = () => Portfolio.Create(new string('a', NameValidator.MaxLength + 1), "usd", CostBasisMethod.Fifo);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowArgumentException_When_CreatedWithBlankCurrency(string? vsCurrency)
    {
        // `!` lets the theory pass null past the non-nullable parameter to exercise the guard.
        var act = () => Portfolio.Create("Long term", vsCurrency!, CostBasisMethod.Fifo);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_ThrowArgumentOutOfRangeException_When_CreatedWithUndefinedMethod()
    {
        var act = () => Portfolio.Create("Long term", "usd", (CostBasisMethod)99);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // Update.

    [Fact]
    public void Should_HaveNewNameAndMethodAndSameCurrency_When_Updated()
    {
        var portfolio = Portfolio.Create("Long term", "usd", CostBasisMethod.Fifo);

        portfolio.Update("Retirement", CostBasisMethod.AverageCost);

        portfolio.Name.Should().Be("Retirement");
        portfolio.CostBasisMethod.Should().Be(CostBasisMethod.AverageCost);
        portfolio.VsCurrency.Should().Be("usd");
    }

    [Fact]
    public void Should_MoveToNextVersion_When_Updated()
    {
        var portfolio = Portfolio.Create("Long term", "usd", CostBasisMethod.Fifo);

        portfolio.Update("Retirement", CostBasisMethod.AverageCost);

        portfolio.Version.Should().Be(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowAndKeepStateAndVersion_When_UpdatedWithBlankName(string? name)
    {
        var portfolio = Portfolio.Create("Long term", "usd", CostBasisMethod.Fifo);

        // `!` lets the theory pass null past the non-nullable parameter to exercise the guard.
        var act = () => portfolio.Update(name!, CostBasisMethod.AverageCost);

        act.Should().Throw<ArgumentException>();
        portfolio.Name.Should().Be("Long term");
        portfolio.CostBasisMethod.Should().Be(CostBasisMethod.Fifo);
        portfolio.Version.Should().Be(1);
    }

    [Fact]
    public void Should_ThrowAndKeepStateAndVersion_When_UpdatedWithNameLongerThanMaxLength()
    {
        var portfolio = Portfolio.Create("Long term", "usd", CostBasisMethod.Fifo);

        var act = () => portfolio.Update(new string('a', NameValidator.MaxLength + 1), CostBasisMethod.AverageCost);

        act.Should().Throw<ArgumentException>();
        portfolio.Name.Should().Be("Long term");
        portfolio.Version.Should().Be(1);
    }

    [Fact]
    public void Should_ThrowAndKeepStateAndVersion_When_UpdatedWithUndefinedMethod()
    {
        var portfolio = Portfolio.Create("Long term", "usd", CostBasisMethod.Fifo);

        var act = () => portfolio.Update("Retirement", (CostBasisMethod)99);

        act.Should().Throw<ArgumentOutOfRangeException>();
        portfolio.Name.Should().Be("Long term");
        portfolio.CostBasisMethod.Should().Be(CostBasisMethod.Fifo);
        portfolio.Version.Should().Be(1);
    }
}
