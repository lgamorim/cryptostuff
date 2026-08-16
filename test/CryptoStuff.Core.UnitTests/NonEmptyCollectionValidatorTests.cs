namespace CryptoStuff.Core.UnitTests;

public class NonEmptyCollectionValidatorTests
{
    [Fact]
    public void Should_ReturnValid_When_CollectionHasAtLeastOneItem()
    {
        var outcome = NonEmptyCollectionValidator.Validate(new[] { "bitcoin" }, "CoinIds");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_CollectionIsNull()
    {
        var outcome = NonEmptyCollectionValidator.Validate(null, "CoinIds");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("CoinIds");
    }

    [Fact]
    public void Should_ReturnInvalidWithFieldNameInMessage_When_CollectionIsEmpty()
    {
        var outcome = NonEmptyCollectionValidator.Validate(Array.Empty<string>(), "CoinIds");

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("CoinIds");
    }

    [Fact]
    public void Should_ReturnValid_When_CollectionContainsOnlyBlankElements()
    {
        var outcome = NonEmptyCollectionValidator.Validate(new[] { "", "   " }, "CoinIds");

        outcome.IsValid.Should().BeTrue();
    }
}
