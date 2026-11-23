namespace CryptoStuff.Composition.UnitTests;

public class CryptoStuffServiceProviderExtensionsTests
{
    [Fact]
    public async Task Should_ThrowArgumentNullException_When_ReadinessServiceProviderIsNull()
    {
        IServiceProvider services = null!; // `!` passes null past the non-nullable receiver to exercise the guard.

        var act = () => services.IsCryptoStuffDatabaseReadyAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("services");
    }
}
