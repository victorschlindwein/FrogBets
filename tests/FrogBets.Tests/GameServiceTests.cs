using FrogBets.Api.Services;
using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FrogBets.Tests;

/// <summary>
/// Unit tests for GameService.
/// Uses InMemory database — transactions are no-ops but logic is fully exercised.
/// </summary>
public class GameServiceTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static FrogBetsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FrogBetsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new FrogBetsDbContext(options);
    }

    private static CreateGameRequest DefaultRequest(int numberOfMaps = 3) =>
        new("TeamA", "TeamB", DateTime.UtcNow.AddDays(1), numberOfMaps);

    /// <summary>No-op settlement service for GameService unit tests that don't test settlement.</summary>
    private sealed class NoOpSettlementService : ISettlementService
    {
        public Task SettleMarketAsync(Guid marketId, string winningOption, bool isVoided = false)
            => Task.CompletedTask;
    }

    private sealed class NoOpBalanceService : IBalanceService
    {
        public Task ReserveBalanceAsync(Guid userId, decimal amount) => Task.CompletedTask;
        public Task ReleaseBalanceAsync(Guid userId, decimal amount) => Task.CompletedTask;
        public Task CreditWinnerAsync(Guid winnerId, decimal amount) => Task.CompletedTask;
    }

    private static GameService CreateGameService(FrogBetsDbContext db)
        => new(db, new NoOpSettlementService(), new NoOpBalanceService());

    // ── CreateGame ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateGame_ReturnsNewGameId()
    {
        await using var db = CreateDb();
        var svc = CreateGameService(db);

        var id = await svc.CreateGameAsync(DefaultRequest());

        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task CreateGame_PersistsGameWithScheduledStatus()
    {
        await using var db = CreateDb();
        var svc = CreateGameService(db);

        var id = await svc.CreateGameAsync(DefaultRequest());

        var game = await db.Games.FindAsync(id);
        Assert.NotNull(game);
        Assert.Equal(GameStatus.Scheduled, game!.Status);
        Assert.Equal("TeamA", game.TeamA);
        Assert.Equal("TeamB", game.TeamB);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CreateGame_CreatesCorrectNumberOfMarkets(int numberOfMaps)
    {
        await using var db = CreateDb();
        var svc = CreateGameService(db);

        var id = await svc.CreateGameAsync(DefaultRequest(numberOfMaps));

        // 4 map-level types per map + 1 SeriesWinner
        var expectedCount = numberOfMaps * 4 + 1;
        var markets = await db.Markets.Where(m => m.GameId == id).ToListAsync();
        Assert.Equal(expectedCount, markets.Count);
    }

    [Fact]
    public async Task CreateGame_CreatesSeriesWinnerMarketWithNullMapNumber()
    {
        await using var db = CreateDb();
        var svc = CreateGameService(db);

        var id = await svc.CreateGameAsync(DefaultRequest(2));

        var seriesMarket = await db.Markets
            .FirstOrDefaultAsync(m => m.GameId == id && m.Type == MarketType.SeriesWinner);

        Assert.NotNull(seriesMarket);
        Assert.Null(seriesMarket!.MapNumber);
    }

    [Fact]
    public async Task CreateGame_CreatesMapMarketsForEachMap()
    {
        await using var db = CreateDb();
        var svc = CreateGameService(db);

        var id = await svc.CreateGameAsync(DefaultRequest(2));

        var mapTypes = new[] { MarketType.MapWinner, MarketType.TopKills, MarketType.MostDeaths, MarketType.MostUtilityDamage };
        foreach (var mapNum in new[] { 1, 2 })
        {
            foreach (var type in mapTypes)
            {
                var exists = await db.Markets.AnyAsync(m =>
                    m.GameId == id && m.Type == type && m.MapNumber == mapNum);
                Assert.True(exists, $"Expected market {type} for map {mapNum}");
            }
        }
    }

    [Fact]
    public async Task CreateGame_AllMarketsAreOpen()
    {
        await using var db = CreateDb();
        var svc = CreateGameService(db);

        var id = await svc.CreateGameAsync(DefaultRequest(2));

        var nonOpen = await db.Markets
            .Where(m => m.GameId == id && m.Status != MarketStatus.Open)
            .CountAsync();
        Assert.Equal(0, nonOpen);
    }

    // ── GetGames ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetGames_ReturnsAllGames()
    {
        await using var db = CreateDb();
        var svc = CreateGameService(db);

        await svc.CreateGameAsync(DefaultRequest());
        await svc.CreateGameAsync(DefaultRequest());

        var games = await svc.GetGamesAsync();
        Assert.Equal(2, games.Count);
    }

    [Fact]
    public async Task GetGames_IncludesMarketsInResponse()
    {
        await using var db = CreateDb();
        var svc = CreateGameService(db);

        await svc.CreateGameAsync(DefaultRequest(1));

        var games = await svc.GetGamesAsync();
        Assert.NotEmpty(games[0].Markets);
    }

    // ── StartGame ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartGame_SetsStatusToInProgress()
    {
        await using var db = CreateDb();
        var svc = CreateGameService(db);
        var id = await svc.CreateGameAsync(DefaultRequest());

        await svc.StartGameAsync(id);

        var game = await db.Games.FindAsync(id);
        Assert.Equal(GameStatus.InProgress, game!.Status);
    }

    [Fact]
    public async Task StartGame_ClosesAllOpenMarkets()
    {
        await using var db = CreateDb();
        var svc = CreateGameService(db);
        var id = await svc.CreateGameAsync(DefaultRequest(2));

        await svc.StartGameAsync(id);

        var openMarkets = await db.Markets
            .Where(m => m.GameId == id && m.Status == MarketStatus.Open)
            .CountAsync();
        Assert.Equal(0, openMarkets);
    }

    [Fact]
    public async Task StartGame_UnknownGame_ThrowsKeyNotFoundException()
    {
        await using var db = CreateDb();
        var svc = CreateGameService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.StartGameAsync(Guid.NewGuid()));
    }

    // ── RegisterResult ────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterResult_SettlesMarket()
    {
        await using var db = CreateDb();
        var svc = CreateGameService(db);
        var gameId = await svc.CreateGameAsync(DefaultRequest(1));
        await svc.StartGameAsync(gameId);

        var market = await db.Markets
            .FirstAsync(m => m.GameId == gameId && m.Type == MarketType.MapWinner);

        var adminId = Guid.NewGuid();
        await svc.RegisterResultAsync(gameId,
            new RegisterResultRequest(market.Id, "TeamA", 1), adminId);

        var updated = await db.Markets.FindAsync(market.Id);
        Assert.Equal(MarketStatus.Settled, updated!.Status);
        Assert.Equal("TeamA", updated.WinningOption);
    }

    [Fact]
    public async Task RegisterResult_CreatesGameResultRecord()
    {
        await using var db = CreateDb();
        var svc = CreateGameService(db);
        var gameId = await svc.CreateGameAsync(DefaultRequest(1));
        await svc.StartGameAsync(gameId);

        var market = await db.Markets
            .FirstAsync(m => m.GameId == gameId && m.Type == MarketType.SeriesWinner);

        var adminId = Guid.NewGuid();
        await svc.RegisterResultAsync(gameId,
            new RegisterResultRequest(market.Id, "TeamB", null), adminId);

        var resultRecord = await db.GameResults
            .FirstOrDefaultAsync(r => r.MarketId == market.Id);
        Assert.NotNull(resultRecord);
        Assert.Equal("TeamB", resultRecord!.WinningOption);
        Assert.Equal(adminId, resultRecord.RegisteredByAdminId);
    }

    [Fact]
    public async Task RegisterResult_AllMarketsSettled_SetsGameToFinished()
    {
        await using var db = CreateDb();
        var svc = CreateGameService(db);
        // 1 map → 4 map markets + 1 series = 5 total
        var gameId = await svc.CreateGameAsync(DefaultRequest(1));
        await svc.StartGameAsync(gameId);

        var adminId = Guid.NewGuid();
        var markets = await db.Markets.Where(m => m.GameId == gameId).ToListAsync();

        foreach (var m in markets)
        {
            await svc.RegisterResultAsync(gameId,
                new RegisterResultRequest(m.Id, "TeamA", m.MapNumber), adminId);
        }

        var game = await db.Games.FindAsync(gameId);
        Assert.Equal(GameStatus.Finished, game!.Status);
    }

    [Fact]
    public async Task RegisterResult_PartialSettlement_GameRemainsInProgress()
    {
        await using var db = CreateDb();
        var svc = CreateGameService(db);
        var gameId = await svc.CreateGameAsync(DefaultRequest(2));
        await svc.StartGameAsync(gameId);

        var adminId = Guid.NewGuid();
        // Settle only one market
        var market = await db.Markets
            .FirstAsync(m => m.GameId == gameId && m.Type == MarketType.MapWinner && m.MapNumber == 1);

        await svc.RegisterResultAsync(gameId,
            new RegisterResultRequest(market.Id, "TeamA", 1), adminId);

        var game = await db.Games.FindAsync(gameId);
        Assert.Equal(GameStatus.InProgress, game!.Status);
    }

    [Fact]
    public async Task RegisterResult_FinishedGame_ThrowsInvalidOperationException()
    {
        await using var db = CreateDb();
        var svc = CreateGameService(db);
        var gameId = await svc.CreateGameAsync(DefaultRequest(1));
        await svc.StartGameAsync(gameId);

        var adminId = Guid.NewGuid();
        var markets = await db.Markets.Where(m => m.GameId == gameId).ToListAsync();

        // Settle all markets to finish the game
        foreach (var m in markets)
        {
            await svc.RegisterResultAsync(gameId,
                new RegisterResultRequest(m.Id, "TeamA", m.MapNumber), adminId);
        }

        // Now try to register another result on the finished game
        var anyMarket = markets.First();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RegisterResultAsync(gameId,
                new RegisterResultRequest(anyMarket.Id, "TeamB", anyMarket.MapNumber), adminId));

        Assert.Equal("GAME_ALREADY_FINISHED", ex.Message);
    }

    [Fact]
    public async Task RegisterResult_UnknownGame_ThrowsKeyNotFoundException()
    {
        await using var db = CreateDb();
        var svc = CreateGameService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.RegisterResultAsync(Guid.NewGuid(),
                new RegisterResultRequest(Guid.NewGuid(), "TeamA", null), Guid.NewGuid()));
    }

    [Fact]
    public async Task RegisterResult_UnknownMarket_ThrowsKeyNotFoundException()
    {
        await using var db = CreateDb();
        var svc = CreateGameService(db);
        var gameId = await svc.CreateGameAsync(DefaultRequest(1));

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.RegisterResultAsync(gameId,
                new RegisterResultRequest(Guid.NewGuid(), "TeamA", null), Guid.NewGuid()));
    }
}
