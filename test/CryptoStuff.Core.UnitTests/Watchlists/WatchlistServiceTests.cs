using CryptoStuff.Core.UnitTests.TestSupport;
using CryptoStuff.Core.Watchlists;

namespace CryptoStuff.Core.UnitTests.Watchlists;

public class WatchlistServiceTests
{
    private const int UnknownId = 999;

    private static (FakeWatchlistRepository Repository, SpyCryptocurrencyService Upstream, WatchlistService Service) CreateSut()
    {
        var repository = new FakeWatchlistRepository();
        var upstream = new SpyCryptocurrencyService();
        return (repository, upstream, new WatchlistService(repository, upstream));
    }

    private static Watchlist SeedWatchlist(FakeWatchlistRepository repository, string name, params string[] coinIds)
    {
        var watchlist = Watchlist.Create(name);
        foreach (var coinId in coinIds)
        {
            watchlist.AddCoin(coinId);
        }

        return repository.Seed(watchlist);
    }

    // ListAsync.

    [Fact]
    public async Task Should_ReturnEveryWatchlistInIdOrder_When_ListAsyncCalled()
    {
        var (repository, _, service) = CreateSut();
        SeedWatchlist(repository, "Majors", "bitcoin");
        SeedWatchlist(repository, "Memes", "dogecoin");

        var result = await service.ListAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        result.Value!.Select(w => w.Name).Should().Equal("Majors", "Memes");
        result.Value![1].CoinIds.Should().Equal("dogecoin");
    }

    [Fact]
    public async Task Should_ReturnEmptyList_When_ListAsyncCalledWithNoWatchlists()
    {
        var (_, _, service) = CreateSut();

        var result = await service.ListAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // GetAsync.

    [Fact]
    public async Task Should_ReturnWatchlist_When_GetAsyncCalledWithExistingId()
    {
        var (repository, _, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors", "bitcoin", "ethereum");

        var result = await service.GetAsync(watchlist.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new { watchlist.Id, Name = "Majors", CoinIds = new[] { "bitcoin", "ethereum" } });
    }

    [Fact]
    public async Task Should_ReturnNotFoundNamingId_When_GetAsyncCalledWithUnknownId()
    {
        var (_, _, service) = CreateSut();

        var result = await service.GetAsync(UnknownId, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        result.ErrorDetail.Should().Contain("999");
    }

    // CreateAsync.

    [Fact]
    public async Task Should_SaveAndReturnWatchlistWithAssignedId_When_CreateAsyncNameIsNew()
    {
        var (repository, _, service) = CreateSut();

        var result = await service.CreateAsync(new CreateWatchlistCommand { Name = "Majors" }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        result.Value!.Id.Should().BePositive();
        result.Value.Name.Should().Be("Majors");
        result.Value.CoinIds.Should().BeEmpty();
        repository.Saved.Should().ContainSingle(w => w.Id == result.Value.Id && w.Name == "Majors");
    }

    [Fact]
    public async Task Should_ReturnConflictNamingNameWithoutSaving_When_CreateAsyncNameAlreadyExists()
    {
        var (repository, _, service) = CreateSut();
        SeedWatchlist(repository, "Majors");

        var result = await service.CreateAsync(new CreateWatchlistCommand { Name = "Majors" }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().Contain("Majors");
        repository.SaveChangesCallCount.Should().Be(0);
        repository.Saved.Should().ContainSingle();
    }

    [Fact]
    public async Task Should_ReturnConflict_When_CreateAsyncNameDiffersFromExistingOnlyInCase()
    {
        var (repository, _, service) = CreateSut();
        SeedWatchlist(repository, "Majors");

        var result = await service.CreateAsync(new CreateWatchlistCommand { Name = "MAJORS" }, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
    }

    [Fact]
    public async Task Should_ReturnConflictNamingName_When_CreateAsyncSaveLosesUniqueNameRace()
    {
        var (repository, _, service) = CreateSut();
        repository.SaveChangesException = new PersistenceConflictException();

        var result = await service.CreateAsync(new CreateWatchlistCommand { Name = "Majors" }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().Contain("Majors");
    }

    [Fact]
    public async Task Should_ThrowArgumentExceptionWithoutSaving_When_CreateAsyncNameIsBlank()
    {
        var (repository, _, service) = CreateSut();

        var act = () => service.CreateAsync(new CreateWatchlistCommand { Name = " " }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        repository.SaveChangesCallCount.Should().Be(0);
    }

    // RenameAsync.

    [Fact]
    public async Task Should_SaveAndReturnRenamedWatchlist_When_RenameAsyncNameIsNew()
    {
        var (repository, _, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors", "bitcoin");

        var result = await service.RenameAsync(new RenameWatchlistCommand { WatchlistId = watchlist.Id, Name = "Large caps" }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        result.Value!.Name.Should().Be("Large caps");
        result.Value.CoinIds.Should().Equal("bitcoin");
        watchlist.Name.Should().Be("Large caps");
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_Succeed_When_RenameAsyncChangesOnlyTheCaseOfItsOwnName()
    {
        var (repository, _, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors");

        var result = await service.RenameAsync(new RenameWatchlistCommand { WatchlistId = watchlist.Id, Name = "MAJORS" }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        result.Value!.Name.Should().Be("MAJORS");
    }

    [Fact]
    public async Task Should_ReturnNotFoundWithoutSaving_When_RenameAsyncIdIsUnknown()
    {
        var (repository, _, service) = CreateSut();

        var result = await service.RenameAsync(new RenameWatchlistCommand { WatchlistId = UnknownId, Name = "Majors" }, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnConflictAndKeepNameWithoutSaving_When_RenameAsyncNameBelongsToAnotherWatchlist()
    {
        var (repository, _, service) = CreateSut();
        SeedWatchlist(repository, "Majors");
        var memes = SeedWatchlist(repository, "Memes");

        var result = await service.RenameAsync(new RenameWatchlistCommand { WatchlistId = memes.Id, Name = "majors" }, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().Contain("majors");
        memes.Name.Should().Be("Memes");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnConflictNamingName_When_RenameAsyncSaveLosesUniqueNameRace()
    {
        var (repository, _, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors");
        repository.SaveChangesException = new PersistenceConflictException();

        var result = await service.RenameAsync(new RenameWatchlistCommand { WatchlistId = watchlist.Id, Name = "Large caps" }, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().Contain("Large caps");
    }

    [Fact]
    public async Task Should_ThrowArgumentExceptionAndKeepNameWithoutSaving_When_RenameAsyncNameIsBlank()
    {
        var (repository, _, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors");

        var act = () => service.RenameAsync(new RenameWatchlistCommand { WatchlistId = watchlist.Id, Name = " " }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        watchlist.Name.Should().Be("Majors");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    // DeleteAsync.

    [Fact]
    public async Task Should_RemoveWatchlist_When_DeleteAsyncIdExists()
    {
        var (repository, _, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors");
        SeedWatchlist(repository, "Memes");

        var result = await service.DeleteAsync(watchlist.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Saved.Should().ContainSingle(w => w.Name == "Memes");
    }

    [Fact]
    public async Task Should_ReturnNotFoundNamingIdWithoutSaving_When_DeleteAsyncIdIsUnknown()
    {
        var (repository, _, service) = CreateSut();

        var result = await service.DeleteAsync(UnknownId, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        result.ErrorDetail.Should().Contain("999");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnConflict_When_DeleteAsyncSaveThrowsPersistenceConflict()
    {
        var (repository, _, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors");
        repository.SaveChangesException = new PersistenceConflictException();

        var result = await service.DeleteAsync(watchlist.Id, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().NotBeNull();
    }

    // AddCoinAsync.

    [Fact]
    public async Task Should_ValidateCoinUpstreamThenAppendAndSave_When_AddCoinAsyncCoinIsKnown()
    {
        var (repository, upstream, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors", "ethereum");

        var result = await service.AddCoinAsync(new WatchlistCoinCommand { WatchlistId = watchlist.Id, CoinId = "bitcoin" }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        result.Value!.CoinIds.Should().Equal("ethereum", "bitcoin");
        upstream.CapturedCoinQuery.Should().Be(new CoinQuery { CoinId = "bitcoin" });
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_ValidateAndStoreLowercasedId_When_AddCoinAsyncCoinHasUppercaseLetters()
    {
        var (repository, upstream, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors");

        var result = await service.AddCoinAsync(new WatchlistCoinCommand { WatchlistId = watchlist.Id, CoinId = "BitCoin" }, CancellationToken.None);

        // `!` is safe: adding a known coin asks CoinGecko about it and then succeeds, and a null
        // query or value would fail the test with a NullReferenceException anyway.
        upstream.CapturedCoinQuery!.CoinId.Should().Be("bitcoin");
        result.Value!.CoinIds.Should().Equal("bitcoin");
    }

    [Fact]
    public async Task Should_ReturnRuleViolatedNamingCoinWithoutSaving_When_AddCoinAsyncCoinIsUnknownToCoinGecko()
    {
        var (repository, upstream, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors");
        upstream.CoinResult = ServiceResult<CoinView>.Failure(ServiceErrorCode.NotFound);

        var result = await service.AddCoinAsync(new WatchlistCoinCommand { WatchlistId = watchlist.Id, CoinId = "notacoin" }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.RuleViolated);
        result.ErrorDetail.Should().Contain("notacoin");
        watchlist.CoinIds.Should().BeEmpty();
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_PropagateUpstreamFailureWithoutSaving_When_AddCoinAsyncValidationIsRateLimited()
    {
        var (repository, upstream, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors");
        upstream.CoinResult = ServiceResult<CoinView>.Failure(ServiceErrorCode.RateLimited, "Slow down.");

        var result = await service.AddCoinAsync(new WatchlistCoinCommand { WatchlistId = watchlist.Id, CoinId = "bitcoin" }, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.RateLimited);
        result.ErrorDetail.Should().Be("Slow down.");
        watchlist.CoinIds.Should().BeEmpty();
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_SucceedWithoutUpstreamCallOrSave_When_AddCoinAsyncCoinIsAlreadyPresent()
    {
        var (repository, upstream, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors", "bitcoin", "ethereum");

        var result = await service.AddCoinAsync(new WatchlistCoinCommand { WatchlistId = watchlist.Id, CoinId = "BITCOIN" }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        result.Value!.CoinIds.Should().Equal("bitcoin", "ethereum");
        upstream.GetCoinCallCount.Should().Be(0);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnNotFoundWithoutUpstreamCall_When_AddCoinAsyncWatchlistIsUnknown()
    {
        var (_, upstream, service) = CreateSut();

        var result = await service.AddCoinAsync(new WatchlistCoinCommand { WatchlistId = UnknownId, CoinId = "bitcoin" }, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        upstream.GetCoinCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnConflict_When_AddCoinAsyncSaveThrowsPersistenceConflict()
    {
        var (repository, _, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors");
        repository.SaveChangesException = new PersistenceConflictException();

        var result = await service.AddCoinAsync(new WatchlistCoinCommand { WatchlistId = watchlist.Id, CoinId = "bitcoin" }, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_ThrowArgumentExceptionWithoutUpstreamCallOrSave_When_AddCoinAsyncCoinIdIsBlank()
    {
        var (repository, upstream, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors");

        var act = () => service.AddCoinAsync(new WatchlistCoinCommand { WatchlistId = watchlist.Id, CoinId = " " }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        upstream.GetCoinCallCount.Should().Be(0);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    // RemoveCoinAsync.

    [Fact]
    public async Task Should_RemoveCoinAndSave_When_RemoveCoinAsyncCoinIsPresent()
    {
        var (repository, upstream, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors", "bitcoin", "ethereum", "solana");

        var result = await service.RemoveCoinAsync(new WatchlistCoinCommand { WatchlistId = watchlist.Id, CoinId = "Ethereum" }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        result.Value!.CoinIds.Should().Equal("bitcoin", "solana");
        repository.SaveChangesCallCount.Should().Be(1);
        upstream.GetCoinCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_SucceedWithoutSaving_When_RemoveCoinAsyncCoinIsAbsent()
    {
        var (repository, _, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors", "bitcoin");

        var result = await service.RemoveCoinAsync(new WatchlistCoinCommand { WatchlistId = watchlist.Id, CoinId = "ethereum" }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        result.Value!.CoinIds.Should().Equal("bitcoin");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_RemoveCoinAsyncWatchlistIsUnknown()
    {
        var (_, _, service) = CreateSut();

        var result = await service.RemoveCoinAsync(new WatchlistCoinCommand { WatchlistId = UnknownId, CoinId = "bitcoin" }, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnConflict_When_RemoveCoinAsyncSaveThrowsPersistenceConflict()
    {
        var (repository, _, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors", "bitcoin");
        repository.SaveChangesException = new PersistenceConflictException();

        var result = await service.RemoveCoinAsync(new WatchlistCoinCommand { WatchlistId = watchlist.Id, CoinId = "bitcoin" }, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_ThrowArgumentExceptionAndKeepCoinsWithoutSaving_When_RemoveCoinAsyncCoinIdIsBlank()
    {
        var (repository, _, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors", "bitcoin");

        var act = () => service.RemoveCoinAsync(new WatchlistCoinCommand { WatchlistId = watchlist.Id, CoinId = " " }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        watchlist.CoinIds.Should().Equal("bitcoin");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    // GetPricesAsync.

    [Fact]
    public async Task Should_PriceWatchlistCoinsInOrderInRequestedCurrency_When_GetPricesAsyncWatchlistHasCoins()
    {
        var (repository, upstream, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors", "ethereum", "bitcoin");
        var prices = new CoinPriceView
        {
            Coins =
            [
                new CoinPrice { CoinId = "ethereum", Amounts = [new CurrencyAmount { Currency = "eur", Amount = 3000m }] },
                new CoinPrice { CoinId = "bitcoin", Amounts = [new CurrencyAmount { Currency = "eur", Amount = 60000m }] },
            ],
        };
        upstream.PricesResult = ServiceResult<CoinPriceView>.Success(prices);

        var result = await service.GetPricesAsync(new WatchlistPriceQuery { WatchlistId = watchlist.Id, VsCurrency = "eur" }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(prices);
        // `!` is safe: the service asked CoinGecko for these prices, so the query was captured.
        upstream.CapturedPricesQuery!.CoinIds.Should().Equal("ethereum", "bitcoin");
        upstream.CapturedPricesQuery.VsCurrencies.Should().Equal("eur");
    }

    [Fact]
    public async Task Should_ReturnEmptyViewWithoutUpstreamCall_When_GetPricesAsyncWatchlistIsEmpty()
    {
        var (repository, upstream, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors");

        var result = await service.GetPricesAsync(new WatchlistPriceQuery { WatchlistId = watchlist.Id, VsCurrency = "usd" }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        result.Value!.Coins.Should().BeEmpty();
        upstream.GetPricesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnNotFoundWithoutUpstreamCall_When_GetPricesAsyncWatchlistIsUnknown()
    {
        var (_, upstream, service) = CreateSut();

        var result = await service.GetPricesAsync(new WatchlistPriceQuery { WatchlistId = UnknownId, VsCurrency = "usd" }, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        upstream.GetPricesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_PropagateRateLimited_When_GetPricesAsyncUpstreamIsRateLimited()
    {
        var (repository, upstream, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors", "bitcoin");
        upstream.PricesResult = ServiceResult<CoinPriceView>.Failure(ServiceErrorCode.RateLimited);

        var result = await service.GetPricesAsync(new WatchlistPriceQuery { WatchlistId = watchlist.Id, VsCurrency = "usd" }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.RateLimited);
    }

    [Fact]
    public async Task Should_ReturnUpstreamUnavailableNamingTheCoins_When_GetPricesAsyncUpstreamDoesNotKnowACoin()
    {
        var (repository, upstream, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors", "bitcoin", "ethereum");
        upstream.PricesResult = ServiceResult<CoinPriceView>.Failure(ServiceErrorCode.NotFound);

        var result = await service.GetPricesAsync(new WatchlistPriceQuery { WatchlistId = watchlist.Id, VsCurrency = "usd" }, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.UpstreamUnavailable);
        result.ErrorDetail.Should().Contain("bitcoin").And.Contain("ethereum");
    }

    // Cancellation.

    [Fact]
    public async Task Should_ForwardCancellationTokenToRepository_When_ListAsyncCalled()
    {
        var (repository, _, service) = CreateSut();
        using var cts = new CancellationTokenSource();

        await service.ListAsync(cts.Token);

        repository.CapturedCancellationTokens.Should().NotBeEmpty().And.OnlyContain(token => token == cts.Token);
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToRepository_When_GetAsyncCalled()
    {
        var (repository, _, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors");
        using var cts = new CancellationTokenSource();

        await service.GetAsync(watchlist.Id, cts.Token);

        repository.CapturedCancellationTokens.Should().NotBeEmpty().And.OnlyContain(token => token == cts.Token);
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToEveryRepositoryCall_When_CreateAsyncCalled()
    {
        var (repository, _, service) = CreateSut();
        using var cts = new CancellationTokenSource();

        await service.CreateAsync(new CreateWatchlistCommand { Name = "Majors" }, cts.Token);

        repository.CapturedCancellationTokens.Should().HaveCount(2).And.OnlyContain(token => token == cts.Token);
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToEveryRepositoryCall_When_RenameAsyncCalled()
    {
        var (repository, _, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors");
        using var cts = new CancellationTokenSource();

        await service.RenameAsync(new RenameWatchlistCommand { WatchlistId = watchlist.Id, Name = "Large caps" }, cts.Token);

        repository.CapturedCancellationTokens.Should().HaveCount(3).And.OnlyContain(token => token == cts.Token);
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToEveryRepositoryCall_When_DeleteAsyncCalled()
    {
        var (repository, _, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors");
        using var cts = new CancellationTokenSource();

        await service.DeleteAsync(watchlist.Id, cts.Token);

        repository.CapturedCancellationTokens.Should().HaveCount(2).And.OnlyContain(token => token == cts.Token);
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToRepositoryAndUpstream_When_AddCoinAsyncCalled()
    {
        var (repository, upstream, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors");
        using var cts = new CancellationTokenSource();

        await service.AddCoinAsync(new WatchlistCoinCommand { WatchlistId = watchlist.Id, CoinId = "bitcoin" }, cts.Token);

        repository.CapturedCancellationTokens.Should().HaveCount(2).And.OnlyContain(token => token == cts.Token);
        upstream.CapturedCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToEveryRepositoryCall_When_RemoveCoinAsyncCalled()
    {
        var (repository, _, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors", "bitcoin");
        using var cts = new CancellationTokenSource();

        await service.RemoveCoinAsync(new WatchlistCoinCommand { WatchlistId = watchlist.Id, CoinId = "bitcoin" }, cts.Token);

        repository.CapturedCancellationTokens.Should().HaveCount(2).And.OnlyContain(token => token == cts.Token);
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToRepositoryAndUpstream_When_GetPricesAsyncCalled()
    {
        var (repository, upstream, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors", "bitcoin");
        using var cts = new CancellationTokenSource();

        await service.GetPricesAsync(new WatchlistPriceQuery { WatchlistId = watchlist.Id, VsCurrency = "usd" }, cts.Token);

        repository.CapturedCancellationTokens.Should().NotBeEmpty().And.OnlyContain(token => token == cts.Token);
        upstream.CapturedCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Should_PropagateCancellationWithoutSaving_When_AddCoinAsyncUpstreamValidationIsCancelled()
    {
        var (repository, upstream, service) = CreateSut();
        var watchlist = SeedWatchlist(repository, "Majors");
        upstream.ExceptionToThrow = new OperationCanceledException();

        var act = () => service.AddCoinAsync(new WatchlistCoinCommand { WatchlistId = watchlist.Id, CoinId = "bitcoin" }, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
        watchlist.CoinIds.Should().BeEmpty();
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_PropagateCancellationRatherThanConflict_When_CreateAsyncSaveIsCancelled()
    {
        var (repository, _, service) = CreateSut();
        repository.SaveChangesException = new OperationCanceledException();

        var act = () => service.CreateAsync(new CreateWatchlistCommand { Name = "Majors" }, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
