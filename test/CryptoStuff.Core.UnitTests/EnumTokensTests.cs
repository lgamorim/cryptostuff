using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoStuff.Core.Alerts;
using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Core.UnitTests;

public class EnumTokensTests
{
    // How both hosts write enumerations in JSON.
    private static readonly JsonSerializerOptions HostJson = new() { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) } };

    [Theory]
    [InlineData(CostBasisMethod.AverageCost, "average-cost")]
    [InlineData(CostBasisMethod.Fifo, "fifo")]
    public void Should_WriteTheLowercaseKebabCaseName_When_MethodTokenized(CostBasisMethod method, string token)
    {
        var written = EnumTokens.ToToken(method);

        written.Should().Be(token);
    }

    // The tokens the database stores, the logs carry and the CLI prints must
    // be the ones the hosts' JSON carries, so every token reads the same
    // wherever it appears.
    [Fact]
    public void Should_WriteWhatTheHostsJsonWrites_When_AnyStoredEnumerationIsTokenized()
    {
        AssertMatchesHostJson<AlertDirection>();
        AssertMatchesHostJson<AlertState>();
        AssertMatchesHostJson<CostBasisMethod>();
        AssertMatchesHostJson<TransactionKind>();
    }

    private static void AssertMatchesHostJson<TEnum>()
        where TEnum : struct, Enum
    {
        foreach (var value in Enum.GetValues<TEnum>())
        {
            var hostJson = JsonSerializer.Serialize(value, HostJson);

            var token = EnumTokens.ToToken(value);

            token.Should().Be(JsonSerializer.Deserialize<string>(hostJson), value.ToString());
        }
    }
}
