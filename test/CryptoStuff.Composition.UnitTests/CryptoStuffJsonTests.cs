using System.Text.Json;
using CryptoStuff.Core;
using CryptoStuff.Core.Alerts;
using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Composition.UnitTests;

public class CryptoStuffJsonTests
{
    [Fact]
    public void Should_ReturnTheOptionsItWasGiven_When_Configuring()
    {
        // Arrange
        var options = new JsonSerializerOptions { WriteIndented = true };

        // Act
        var configured = CryptoStuffJson.Configure(options);

        // Assert
        configured.Should().BeSameAs(options);
        configured.WriteIndented.Should().BeTrue();
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_OptionsIsNull()
    {
        // `!` is deliberate: the test passes the null the signature forbids.
        var act = () => CryptoStuffJson.Configure(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    // The database stores, the logs carry and the CLI prints EnumTokens'
    // tokens, so the hosts' JSON must carry the same ones.
    [Fact]
    public void Should_WriteEachStoredEnumerationAsItsToken_When_Serializing()
    {
        AssertWritesTokens<AlertDirection>();
        AssertWritesTokens<AlertState>();
        AssertWritesTokens<CostBasisMethod>();
        AssertWritesTokens<TransactionKind>();
    }

    [Fact]
    public void Should_ReadEachStoredEnumerationFromItsToken_When_Deserializing()
    {
        AssertReadsTokens<AlertDirection>();
        AssertReadsTokens<AlertState>();
        AssertReadsTokens<CostBasisMethod>();
        AssertReadsTokens<TransactionKind>();
    }

    [Fact]
    public void Should_WriteAnEnumerationAsText_When_ItIsAPropertyValue()
    {
        // Arrange
        var options = CryptoStuffJson.Configure(new JsonSerializerOptions());

        // Act
        var json = JsonSerializer.Serialize(new { Method = CostBasisMethod.AverageCost }, options);

        // Assert
        json.Should().Be("{\"Method\":\"average-cost\"}");
    }

    private static void AssertWritesTokens<TEnum>()
        where TEnum : struct, Enum
    {
        var options = CryptoStuffJson.Configure(new JsonSerializerOptions());

        foreach (var value in Enum.GetValues<TEnum>())
        {
            var json = JsonSerializer.Serialize(value, options);

            json.Should().Be($"\"{EnumTokens.ToToken(value)}\"", value.ToString());
        }
    }

    private static void AssertReadsTokens<TEnum>()
        where TEnum : struct, Enum
    {
        var options = CryptoStuffJson.Configure(new JsonSerializerOptions());

        foreach (var value in Enum.GetValues<TEnum>())
        {
            var read = JsonSerializer.Deserialize<TEnum>($"\"{EnumTokens.ToToken(value)}\"", options);

            read.Should().Be(value);
        }
    }
}
