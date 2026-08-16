namespace CryptoStuff.Core.UnitTests;

public class TokenPriceMapperTests
{
    [Fact]
    public void Should_MapMatrixToPerTokenPriceLists_When_AllRequestedAddressesArePresent()
    {
        var matrix = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["0xaaa"] = new() { ["usd"] = 1m },
            ["0xbbb"] = new() { ["usd"] = 2m },
        };

        var view = TokenPriceMapper.ToView(matrix, ["0xaaa", "0xbbb"], ["usd"]);

        view.Tokens.Should().HaveCount(2);
        view.Tokens[0].ContractAddress.Should().Be("0xaaa");
        view.Tokens[0].Amounts.Should().ContainSingle(a => a.Currency == "usd" && a.Amount == 1m);
        view.Tokens[1].ContractAddress.Should().Be("0xbbb");
        view.Tokens[1].Amounts.Should().ContainSingle(a => a.Currency == "usd" && a.Amount == 2m);
    }

    [Fact]
    public void Should_PreserveRequestedAddressOrder_When_MappingMultipleTokens()
    {
        var matrix = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["0xbbb"] = new() { ["usd"] = 2m },
            ["0xaaa"] = new() { ["usd"] = 1m },
        };

        var view = TokenPriceMapper.ToView(matrix, ["0xaaa", "0xbbb"], ["usd"]);

        view.Tokens.Select(t => t.ContractAddress).Should().Equal("0xaaa", "0xbbb");
    }

    [Fact]
    public void Should_PreserveRequestedCurrencyOrder_When_MappingAmounts()
    {
        var matrix = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["0xaaa"] = new() { ["eur"] = 1m, ["usd"] = 2m },
        };

        var view = TokenPriceMapper.ToView(matrix, ["0xaaa"], ["usd", "eur"]);

        view.Tokens[0].Amounts.Select(a => a.Currency).Should().Equal("usd", "eur");
    }

    [Fact]
    public void Should_OmitCurrencyFromAmounts_When_CurrencyMissingForAPresentAddress()
    {
        var matrix = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["0xaaa"] = new() { ["usd"] = 1m },
        };

        var view = TokenPriceMapper.ToView(matrix, ["0xaaa"], ["usd", "eur"]);

        view.Tokens[0].Amounts.Should().ContainSingle();
        view.Tokens[0].Amounts[0].Currency.Should().Be("usd");
    }

    [Fact]
    public void Should_ReturnEmptyTokensList_When_NoAddressesRequested()
    {
        var matrix = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["0xaaa"] = new() { ["usd"] = 1m },
        };

        var view = TokenPriceMapper.ToView(matrix, [], ["usd"]);

        view.Tokens.Should().BeEmpty();
    }

    [Fact]
    public void Should_TreatTokenAsHavingNoAmounts_When_MatrixValueForThatAddressIsExplicitlyNull()
    {
        var matrix = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["0xaaa"] = null!,
        };

        var view = TokenPriceMapper.ToView(matrix, ["0xaaa"], ["usd"]);

        view.Tokens.Should().ContainSingle();
        view.Tokens[0].Amounts.Should().BeEmpty();
    }
}
