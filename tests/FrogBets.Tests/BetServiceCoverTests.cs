using FrogBets.Api.Services;
using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FrogBets.Tests;

/// <summary>
/// Unit tests for BetService.CoverBetAsync.
/// Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6
/// </summary>
public class BetServiceCoverTests
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

    private static async Task<User> SeedUserAsync(FrogBetsDbContext db, decimal virtualBalance = 1000m)
    {
        var user = new User
        {
            Id             = Guid.NewGuid(),
            Username       = Guid.NewGuid().ToString("N"),
            PasswordHash   = "hash",
            VirtualBalance = virtualBalance,
            CreatedAt      = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<(Game game, Market market)> SeedGameWithMarketAsync(
        FrogBetsDbContext db,
        MarketType marketType = MarketType.MapWinner)
    {
        var game = new Game
        {
            Id           = Guid.NewGuid(),
            TeamA        = "Team A",
            TeamB        = "Team B",
            ScheduledAt  = DateTime.UtcNow.AddDays(1),
            NumberOfMaps = 1,
            Status       = GameStatus.Scheduled,
            CreatedAt    = DateTime.UtcNow,
        };

        var market = new Market
        {
            Id        = Guid.NewGuid(),
            GameId    = game.Id,
            Type      = marketType,
            MapNumber = marketType == MarketType.SeriesWinner ? null : 1,
            Status    = MarketStatus.Open,
            Game      = game,
        };

        game.Markets.Add(market);
        db.Games.Add(game);
        await db.SaveChangesAsync();

        return (game, market);
    }

    private static async Task<Bet> SeedPendingBetAsync(
        FrogBetsDbContext db,
        Guid creatorId,
        Guid marketId,
        string creatorOption = "TeamA",
        decimal amount = 100m,
        BetStatus status = BetStatus.Pending)
    {
        var bet = new Bet
        {
            Id            = Guid.NewGuid(),
            MarketId      = marketId,
            CreatorId     = creatorId,
            CreatorOption = creatorOption,
            Amount        = amount,
            Status        = status,
            CreatedAt     = DateTime.UtcNow,
        };
        db.Bets.Add(bet);
        await db.SaveChangesAsync();
        return bet;
    }

    private static BetService CreateService(FrogBetsDbContext db)
        => new BetService(db, new BalanceService(db));

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CoverBet_ValidCoverer_BetBecomesActive()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, 500m);
        var coverer = await SeedUserAsync(db, 500m);
        var (_, market) = await SeedGameWithMarketAsync(db);
        var bet = await SeedPendingBetAsync(db, creator.Id, market.Id, "TeamA", 100m);
        var svc = CreateService(db);

        await svc.CoverBetAsync(coverer.Id, bet.Id);

        var updated = await db.Bets.FindAsync(bet.Id);
        Assert.Equal(BetStatus.Active, updated!.Status);
    }

    [Fact]
    public async Task CoverBet_ValidCoverer_SetsCoveredByIdAndCoveredAt()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, 500m);
        var coverer = await SeedUserAsync(db, 500m);
        var (_, market) = await SeedGameWithMarketAsync(db);
        var bet = await SeedPendingBetAsync(db, creator.Id, market.Id, "TeamA", 100m);
        var svc = CreateService(db);

        await svc.CoverBetAsync(coverer.Id, bet.Id);

        var updated = await db.Bets.FindAsync(bet.Id);
        Assert.Equal(coverer.Id, updated!.CoveredById);
        Assert.NotNull(updated.CoveredAt);
    }

    [Fact]
    public async Task CoverBet_ValidCoverer_ReservesCovererBalance()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, 500m);
        var coverer = await SeedUserAsync(db, 500m);
        var (_, market) = await SeedGameWithMarketAsync(db);
        var bet = await SeedPendingBetAsync(db, creator.Id, market.Id, "TeamA", 150m);
        var svc = CreateService(db);

        await svc.CoverBetAsync(coverer.Id, bet.Id);

        var updatedCoverer = await db.Users.FindAsync(coverer.Id);
        Assert.Equal(350m, updatedCoverer!.VirtualBalance);
        Assert.Equal(150m, updatedCoverer.ReservedBalance);
    }

    [Fact]
    public async Task CoverBet_ValidCoverer_CreatesNotificationForCreator()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, 500m);
        var coverer = await SeedUserAsync(db, 500m);
        var (_, market) = await SeedGameWithMarketAsync(db);
        var bet = await SeedPendingBetAsync(db, creator.Id, market.Id, "TeamA", 100m);
        var svc = CreateService(db);

        await svc.CoverBetAsync(coverer.Id, bet.Id);

        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.UserId == creator.Id);
        Assert.NotNull(notification);
        Assert.Equal("Sua aposta foi coberta!", notification.Message);
        Assert.False(notification.IsRead);
    }

    // ── Opposite option — team markets ────────────────────────────────────────

    [Theory]
    [InlineData(MarketType.MapWinner, "TeamA", "TeamB")]
    [InlineData(MarketType.MapWinner, "TeamB", "TeamA")]
    [InlineData(MarketType.SeriesWinner, "TeamA", "TeamB")]
    [InlineData(MarketType.SeriesWinner, "TeamB", "TeamA")]
    public async Task CoverBet_TeamMarket_AssignsOppositeTeamOption(
        MarketType marketType, string creatorOption, string expectedCovererOption)
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, 500m);
        var coverer = await SeedUserAsync(db, 500m);
        var (_, market) = await SeedGameWithMarketAsync(db, marketType);
        var bet = await SeedPendingBetAsync(db, creator.Id, market.Id, creatorOption, 100m);
        var svc = CreateService(db);

        await svc.CoverBetAsync(coverer.Id, bet.Id);

        var updated = await db.Bets.FindAsync(bet.Id);
        Assert.Equal(expectedCovererOption, updated!.CovererOption);
    }

    // ── Opposite option — player markets ─────────────────────────────────────

    [Theory]
    [InlineData(MarketType.TopKills, "player1", "NOT_player1")]
    [InlineData(MarketType.MostDeaths, "player2", "NOT_player2")]
    [InlineData(MarketType.MostUtilityDamage, "player3", "NOT_player3")]
    [InlineData(MarketType.TopKills, "NOT_player1", "player1")]
    public async Task CoverBet_PlayerMarket_AssignsNotPrefixedOption(
        MarketType marketType, string creatorOption, string expectedCovererOption)
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, 500m);
        var coverer = await SeedUserAsync(db, 500m);
        var (_, market) = await SeedGameWithMarketAsync(db, marketType);
        var bet = await SeedPendingBetAsync(db, creator.Id, market.Id, creatorOption, 100m);
        var svc = CreateService(db);

        await svc.CoverBetAsync(coverer.Id, bet.Id);

        var updated = await db.Bets.FindAsync(bet.Id);
        Assert.Equal(expectedCovererOption, updated!.CovererOption);
    }

    // ── Self-coverage rejection ───────────────────────────────────────────────

    [Fact]
    public async Task CoverBet_CreatorTriesToCoverOwnBet_ThrowsCannotCoverOwnBet()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, 500m);
        var (_, market) = await SeedGameWithMarketAsync(db);
        var bet = await SeedPendingBetAsync(db, creator.Id, market.Id);
        var svc = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CoverBetAsync(creator.Id, bet.Id));

        Assert.Equal("CANNOT_COVER_OWN_BET", ex.Message);
    }

    [Fact]
    public async Task CoverBet_CreatorTriesToCoverOwnBet_BetRemainsUnchanged()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, 500m);
        var (_, market) = await SeedGameWithMarketAsync(db);
        var bet = await SeedPendingBetAsync(db, creator.Id, market.Id);
        var svc = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CoverBetAsync(creator.Id, bet.Id));

        var unchanged = await db.Bets.FindAsync(bet.Id);
        Assert.Equal(BetStatus.Pending, unchanged!.Status);
        Assert.Null(unchanged.CoveredById);
    }

    // ── Non-pending bet rejection ─────────────────────────────────────────────

    [Theory]
    [InlineData(BetStatus.Active)]
    [InlineData(BetStatus.Settled)]
    [InlineData(BetStatus.Cancelled)]
    [InlineData(BetStatus.Voided)]
    public async Task CoverBet_NonPendingBet_ThrowsBetNotAvailable(BetStatus status)
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, 500m);
        var coverer = await SeedUserAsync(db, 500m);
        var (_, market) = await SeedGameWithMarketAsync(db);
        var bet = await SeedPendingBetAsync(db, creator.Id, market.Id, status: status);
        var svc = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CoverBetAsync(coverer.Id, bet.Id));

        Assert.Equal("BET_NOT_AVAILABLE", ex.Message);
    }

    // ── Insufficient balance ──────────────────────────────────────────────────

    [Fact]
    public async Task CoverBet_InsufficientBalance_ThrowsInsufficientBalance()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, 500m);
        var coverer = await SeedUserAsync(db, 50m);  // not enough
        var (_, market) = await SeedGameWithMarketAsync(db);
        var bet = await SeedPendingBetAsync(db, creator.Id, market.Id, amount: 100m);
        var svc = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CoverBetAsync(coverer.Id, bet.Id));

        Assert.Equal("INSUFFICIENT_BALANCE", ex.Message);
    }

    [Fact]
    public async Task CoverBet_InsufficientBalance_BetRemainsUnchanged()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, 500m);
        var coverer = await SeedUserAsync(db, 50m);
        var (_, market) = await SeedGameWithMarketAsync(db);
        var bet = await SeedPendingBetAsync(db, creator.Id, market.Id, amount: 100m);
        var svc = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CoverBetAsync(coverer.Id, bet.Id));

        var unchanged = await db.Bets.FindAsync(bet.Id);
        Assert.Equal(BetStatus.Pending, unchanged!.Status);
        Assert.Null(unchanged.CoveredById);
    }

    // ── Bet not found ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CoverBet_UnknownBet_ThrowsKeyNotFoundException()
    {
        await using var db = CreateDb();
        var coverer = await SeedUserAsync(db, 500m);
        var svc = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.CoverBetAsync(coverer.Id, Guid.NewGuid()));
    }
}
