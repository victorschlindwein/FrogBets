using FrogBets.Api.Services;
using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FrogBets.Tests;

/// <summary>
/// Unit tests for SettlementService.
/// Uses InMemory database — transactions are no-ops but logic is fully exercised.
/// </summary>
public class SettlementServiceTests
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

    private static async Task<User> SeedUserAsync(FrogBetsDbContext db,
        decimal virtualBalance = 0m, decimal reservedBalance = 100m)
    {
        var user = new User
        {
            Id              = Guid.NewGuid(),
            Username        = Guid.NewGuid().ToString("N"),
            PasswordHash    = "hash",
            VirtualBalance  = virtualBalance,
            ReservedBalance = reservedBalance,
            CreatedAt       = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<(Game game, Market market)> SeedGameAndMarketAsync(
        FrogBetsDbContext db, MarketStatus marketStatus = MarketStatus.Settled)
    {
        var game = new Game
        {
            Id           = Guid.NewGuid(),
            TeamA        = "TeamA",
            TeamB        = "TeamB",
            ScheduledAt  = DateTime.UtcNow.AddDays(1),
            NumberOfMaps = 1,
            Status       = GameStatus.InProgress,
            CreatedAt    = DateTime.UtcNow,
        };
        var market = new Market
        {
            Id            = Guid.NewGuid(),
            GameId        = game.Id,
            Type          = MarketType.MapWinner,
            MapNumber     = 1,
            Status        = marketStatus,
            WinningOption = marketStatus == MarketStatus.Settled ? "TeamA" : null,
        };
        game.Markets.Add(market);
        db.Games.Add(game);
        await db.SaveChangesAsync();
        return (game, market);
    }

    private static async Task<Bet> SeedActiveBetAsync(FrogBetsDbContext db,
        Guid marketId, Guid creatorId, Guid coveredById,
        string creatorOption = "TeamA", decimal amount = 100m)
    {
        var bet = new Bet
        {
            Id            = Guid.NewGuid(),
            MarketId      = marketId,
            CreatorId     = creatorId,
            CoveredById   = coveredById,
            CreatorOption = creatorOption,
            CovererOption = creatorOption == "TeamA" ? "TeamB" : "TeamA",
            Amount        = amount,
            Status        = BetStatus.Active,
            CreatedAt     = DateTime.UtcNow,
            CoveredAt     = DateTime.UtcNow,
        };
        db.Bets.Add(bet);
        await db.SaveChangesAsync();
        return bet;
    }

    private static SettlementService CreateService(FrogBetsDbContext db)
        => new(db, new BalanceService(db));

    // ── Normal settlement — creator wins ─────────────────────────────────────

    [Fact]
    public async Task SettleMarket_CreatorWins_BetStatusIsSettled()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, virtualBalance: 0m, reservedBalance: 100m);
        var coverer = await SeedUserAsync(db, virtualBalance: 0m, reservedBalance: 100m);
        var (_, market) = await SeedGameAndMarketAsync(db);
        var bet = await SeedActiveBetAsync(db, market.Id, creator.Id, coverer.Id, "TeamA", 100m);

        var svc = CreateService(db);
        await svc.SettleMarketAsync(market.Id, "TeamA");

        var updated = await db.Bets.FindAsync(bet.Id);
        Assert.Equal(BetStatus.Settled, updated!.Status);
        Assert.Equal(BetResult.CreatorWon, updated.Result);
        Assert.NotNull(updated.SettledAt);
    }

    [Fact]
    public async Task SettleMarket_CreatorWins_CreatorBalanceCredited()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, virtualBalance: 0m, reservedBalance: 100m);
        var coverer = await SeedUserAsync(db, virtualBalance: 0m, reservedBalance: 100m);
        var (_, market) = await SeedGameAndMarketAsync(db);
        await SeedActiveBetAsync(db, market.Id, creator.Id, coverer.Id, "TeamA", 100m);

        var svc = CreateService(db);
        await svc.SettleMarketAsync(market.Id, "TeamA");

        var updatedCreator = await db.Users.FindAsync(creator.Id);
        // CreditWinner: VirtualBalance += 2*100 = 200, ReservedBalance -= 100 = 0
        Assert.Equal(200m, updatedCreator!.VirtualBalance);
        Assert.Equal(0m, updatedCreator.ReservedBalance);
    }

    [Fact]
    public async Task SettleMarket_CreatorWins_CovererReservedDeducted()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, virtualBalance: 0m, reservedBalance: 100m);
        var coverer = await SeedUserAsync(db, virtualBalance: 0m, reservedBalance: 100m);
        var (_, market) = await SeedGameAndMarketAsync(db);
        await SeedActiveBetAsync(db, market.Id, creator.Id, coverer.Id, "TeamA", 100m);

        var svc = CreateService(db);
        await svc.SettleMarketAsync(market.Id, "TeamA");

        var updatedCoverer = await db.Users.FindAsync(coverer.Id);
        // Loser's reserved is deducted (consumed by winner)
        Assert.Equal(0m, updatedCoverer!.ReservedBalance);
        Assert.Equal(0m, updatedCoverer.VirtualBalance);
    }

    [Fact]
    public async Task SettleMarket_CreatorWins_WinsLossesCountersUpdated()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, virtualBalance: 0m, reservedBalance: 100m);
        var coverer = await SeedUserAsync(db, virtualBalance: 0m, reservedBalance: 100m);
        var (_, market) = await SeedGameAndMarketAsync(db);
        await SeedActiveBetAsync(db, market.Id, creator.Id, coverer.Id, "TeamA", 100m);

        var svc = CreateService(db);
        await svc.SettleMarketAsync(market.Id, "TeamA");

        var updatedCreator = await db.Users.FindAsync(creator.Id);
        var updatedCoverer = await db.Users.FindAsync(coverer.Id);
        Assert.Equal(1, updatedCreator!.WinsCount);
        Assert.Equal(0, updatedCreator.LossesCount);
        Assert.Equal(0, updatedCoverer!.WinsCount);
        Assert.Equal(1, updatedCoverer.LossesCount);
    }

    // ── Normal settlement — coverer wins ─────────────────────────────────────

    [Fact]
    public async Task SettleMarket_CovererWins_BetResultIsCovererWon()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, virtualBalance: 0m, reservedBalance: 100m);
        var coverer = await SeedUserAsync(db, virtualBalance: 0m, reservedBalance: 100m);
        var (_, market) = await SeedGameAndMarketAsync(db);
        var bet = await SeedActiveBetAsync(db, market.Id, creator.Id, coverer.Id, "TeamA", 100m);

        var svc = CreateService(db);
        await svc.SettleMarketAsync(market.Id, "TeamB"); // coverer chose TeamB

        var updated = await db.Bets.FindAsync(bet.Id);
        Assert.Equal(BetResult.CovererWon, updated!.Result);
        Assert.Equal(BetStatus.Settled, updated.Status);
    }

    [Fact]
    public async Task SettleMarket_CovererWins_CovererBalanceCredited()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, virtualBalance: 0m, reservedBalance: 100m);
        var coverer = await SeedUserAsync(db, virtualBalance: 0m, reservedBalance: 100m);
        var (_, market) = await SeedGameAndMarketAsync(db);
        await SeedActiveBetAsync(db, market.Id, creator.Id, coverer.Id, "TeamA", 100m);

        var svc = CreateService(db);
        await svc.SettleMarketAsync(market.Id, "TeamB");

        var updatedCoverer = await db.Users.FindAsync(coverer.Id);
        Assert.Equal(200m, updatedCoverer!.VirtualBalance);
        Assert.Equal(0m, updatedCoverer.ReservedBalance);
    }

    // ── Voided market ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SettleMarket_Voided_BetStatusIsVoided()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, virtualBalance: 0m, reservedBalance: 100m);
        var coverer = await SeedUserAsync(db, virtualBalance: 0m, reservedBalance: 100m);
        var (_, market) = await SeedGameAndMarketAsync(db, MarketStatus.Voided);
        var bet = await SeedActiveBetAsync(db, market.Id, creator.Id, coverer.Id, "TeamA", 100m);

        var svc = CreateService(db);
        await svc.SettleMarketAsync(market.Id, "draw", isVoided: true);

        var updated = await db.Bets.FindAsync(bet.Id);
        Assert.Equal(BetStatus.Voided, updated!.Status);
        Assert.Equal(BetResult.Voided, updated.Result);
    }

    [Fact]
    public async Task SettleMarket_Voided_BothUsersGetRefund()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, virtualBalance: 50m, reservedBalance: 100m);
        var coverer = await SeedUserAsync(db, virtualBalance: 50m, reservedBalance: 100m);
        var (_, market) = await SeedGameAndMarketAsync(db, MarketStatus.Voided);
        await SeedActiveBetAsync(db, market.Id, creator.Id, coverer.Id, "TeamA", 100m);

        var svc = CreateService(db);
        await svc.SettleMarketAsync(market.Id, "draw", isVoided: true);

        var updatedCreator = await db.Users.FindAsync(creator.Id);
        var updatedCoverer = await db.Users.FindAsync(coverer.Id);
        // ReleaseBalance: VirtualBalance += 100, ReservedBalance -= 100
        Assert.Equal(150m, updatedCreator!.VirtualBalance);
        Assert.Equal(0m, updatedCreator.ReservedBalance);
        Assert.Equal(150m, updatedCoverer!.VirtualBalance);
        Assert.Equal(0m, updatedCoverer.ReservedBalance);
    }

    // ── Multiple bets ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SettleMarket_MultipleBets_AllSettled()
    {
        await using var db = CreateDb();
        var (_, market) = await SeedGameAndMarketAsync(db);

        for (var i = 0; i < 3; i++)
        {
            var c = await SeedUserAsync(db, virtualBalance: 0m, reservedBalance: 100m);
            var v = await SeedUserAsync(db, virtualBalance: 0m, reservedBalance: 100m);
            await SeedActiveBetAsync(db, market.Id, c.Id, v.Id, "TeamA", 100m);
        }

        var svc = CreateService(db);
        await svc.SettleMarketAsync(market.Id, "TeamA");

        var unsettled = await db.Bets
            .Where(b => b.MarketId == market.Id && b.Status == BetStatus.Active)
            .CountAsync();
        Assert.Equal(0, unsettled);
    }

    // ── Game status → Finished ────────────────────────────────────────────────

    [Fact]
    public async Task SettleMarket_AllMarketsSettled_GameStatusUnchangedBySettlementService()
    {
        // After the refactor, GameService is responsible for setting game status to Finished.
        // SettlementService only settles bets — it no longer touches game status.
        await using var db = CreateDb();
        var game = new Game
        {
            Id           = Guid.NewGuid(),
            TeamA        = "TeamA",
            TeamB        = "TeamB",
            ScheduledAt  = DateTime.UtcNow.AddDays(1),
            NumberOfMaps = 1,
            Status       = GameStatus.InProgress,
            CreatedAt    = DateTime.UtcNow,
        };
        var market = new Market
        {
            Id            = Guid.NewGuid(),
            GameId        = game.Id,
            Type          = MarketType.MapWinner,
            MapNumber     = 1,
            Status        = MarketStatus.Settled,
            WinningOption = "TeamA",
        };
        game.Markets.Add(market);
        db.Games.Add(game);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.SettleMarketAsync(market.Id, "TeamA");

        // SettlementService no longer sets game to Finished — GameService does that.
        var updatedGame = await db.Games.FindAsync(game.Id);
        Assert.Equal(GameStatus.InProgress, updatedGame!.Status);
    }

    [Fact]
    public async Task SettleMarket_NotAllMarketsSettled_GameRemainsInProgress()
    {
        await using var db = CreateDb();
        var game = new Game
        {
            Id           = Guid.NewGuid(),
            TeamA        = "TeamA",
            TeamB        = "TeamB",
            ScheduledAt  = DateTime.UtcNow.AddDays(1),
            NumberOfMaps = 1,
            Status       = GameStatus.InProgress,
            CreatedAt    = DateTime.UtcNow,
        };
        var market1 = new Market
        {
            Id        = Guid.NewGuid(),
            GameId    = game.Id,
            Type      = MarketType.MapWinner,
            MapNumber = 1,
            Status    = MarketStatus.Settled,
            WinningOption = "TeamA",
        };
        var market2 = new Market
        {
            Id        = Guid.NewGuid(),
            GameId    = game.Id,
            Type      = MarketType.SeriesWinner,
            MapNumber = null,
            Status    = MarketStatus.Closed, // not yet settled
        };
        game.Markets.Add(market1);
        game.Markets.Add(market2);
        db.Games.Add(game);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.SettleMarketAsync(market1.Id, "TeamA");

        var updatedGame = await db.Games.FindAsync(game.Id);
        Assert.Equal(GameStatus.InProgress, updatedGame!.Status);
    }

    // ── Unknown market ────────────────────────────────────────────────────────

    [Fact]
    public async Task SettleMarket_UnknownMarket_ThrowsKeyNotFoundException()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.SettleMarketAsync(Guid.NewGuid(), "TeamA"));
    }

    // ── Pending bets are not settled ──────────────────────────────────────────

    [Fact]
    public async Task SettleMarket_PendingBetsIgnored_OnlyActiveBetsSettled()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, virtualBalance: 100m, reservedBalance: 100m);
        var (_, market) = await SeedGameAndMarketAsync(db);

        // Seed a Pending bet (no coverer)
        var pendingBet = new Bet
        {
            Id            = Guid.NewGuid(),
            MarketId      = market.Id,
            CreatorId     = creator.Id,
            CreatorOption = "TeamA",
            Amount        = 100m,
            Status        = BetStatus.Pending,
            CreatedAt     = DateTime.UtcNow,
        };
        db.Bets.Add(pendingBet);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.SettleMarketAsync(market.Id, "TeamA");

        var unchanged = await db.Bets.FindAsync(pendingBet.Id);
        Assert.Equal(BetStatus.Pending, unchanged!.Status);
    }
}
