using CryptoStuff.Core;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CryptoStuff.Persistence;

/// <summary>
/// Stores an enumeration as the token <see cref="EnumTokens"/> writes, and
/// reads one back through that enumeration's token parser in Core, so a token
/// this version does not know fails the read instead of becoming some value.
/// </summary>
internal static class TokenConverter
{
    /// <summary>The shape of each Core token parser's <c>TryParse</c>.</summary>
    public delegate bool TryParse<TEnum>(string? token, out TEnum value);

    /// <summary>
    /// The value converter for <typeparamref name="TEnum"/>. Reading a token
    /// <paramref name="tryParse"/> rejects throws an
    /// <see cref="InvalidDataException"/> naming the token and
    /// <paramref name="description"/>, such as <c>alert direction</c>.
    /// </summary>
    public static ValueConverter<TEnum, string> For<TEnum>(TryParse<TEnum> tryParse, string description)
        where TEnum : struct, Enum =>
        new(value => EnumTokens.ToToken(value), token => Parse(token, tryParse, description));

    private static TEnum Parse<TEnum>(string token, TryParse<TEnum> tryParse, string description)
        where TEnum : struct, Enum =>
        tryParse(token, out var value)
            ? value
            : throw new InvalidDataException($"'{token}' is not a stored {description}.");
}
