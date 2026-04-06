using FrogBets.Api.Services;
using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FrogBets.Tests;

/// <summary>
/// Unit tests for BetService.CancelBetAsync.
/// Requirements: 6.1, 6.2, 6.3, 6.4
/// </summary>
public class BetServiceCancelTests
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
            ReservedBalance = 0m,
            CreatedAt      = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Market> SeedMarketAsync(FrogBetsDbContext db)
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
            Type      = MarketType.MapWinner,
            MapNumber = 1,
            Status    = MarketStatus.Open,
            Game      = game,
        };
        game.Markets.Add(market);
        db.Games.Add(game);
        await db.SaveChangesAsync();
        return market;
    }

    private static async Task<Bet> SeedBetAsync(
        FrogBetsDbContext db,
        Guid creatorId,
        Guid marketId,
        decimal amount = 100m,
        BetStatus status = BetStatus.Pending)
    {
        var bet = new Bet
        {
            Id            = Guid.NewGuid(),
            MarketId      = marketId,
            CreatorId     = creatorId,
            CreatorOption = "TeamA",
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
    public async Task CancelBet_PendingBet_StatusBecomeCancelled()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, 900m);
        // Simulate reserved balance from bet creation
        creator.ReservedBalance = 100m;
        await db.SaveChangesAsync();

        var market = await SeedMarketAsync(db);
        var bet = await SeedBetAsync(db, creator.Id, market.Id, 100m);
        var svc = CreateService(db);

        await svc.CancelBetAsync(creator.Id, bet.Id);

        var updated = await db.Bets.FindAsync(bet.Id);
        Assert.Equal(BetStatus.Cancelled, updated!.Status);
    }

    [Fact]
    public async Task CancelBet_PendingBet_ReleasesBalanceToCreator()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, 900m);
        creator.ReservedBalance = 100m;
        await db.SaveChangesAsync();

        var market = await SeedMarketAsync(db);
        var bet = await SeedBetAsync(db, creator.Id, market.Id, 100m);
        var svc = CreateService(db);

        await svc.CancelBetAsync(creator.Id, bet.Id);

        var updatedUser = await db.Users.FindAsync(creator.Id);
        Assert.Equal(1000m, updatedUser!.VirtualBalance);
        Assert.Equal(0m, updatedUser.ReservedBalance);
    }

    [Fact]
    public async Task CancelBet_PendingBet_BetRemovedFromPendingListing()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, 900m);
        creator.ReservedBalance = 100m;
        await db.SaveChangesAsync();

        var market = await SeedMarketAsync(db);
        var bet = await SeedBetAsync(db, creator.Id, market.Id, 100m);
        var svc = CreateService(db);

        await svc.CancelBetAsync(creator.Id, bet.Id);

        var pendingBets = await db.Bets
            .Where(b => b.Status == BetStatus.Pending)
            .ToListAsync();
        Assert.DoesNotContain(pendingBets, b => b.Id == bet.Id);
    }

    // ── NOT_BET_OWNER rejection ───────────────────────────────────────────────

    [Fact]
    public async Task CancelBet_NonCreatorUser_ThrowsNotBetOwner()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db);
        var otherUser = await SeedUserAsync(db);
        var market = await SeedMarketAsync(db);
        var bet = await SeedBetAsync(db, creator.Id, market.Id);
        var svc = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CancelBetAsync(otherUser.Id, bet.Id));

        Assert.Equal("NOT_BET_OWNER", ex.Message);
    }

    [Fact]
    public async Task CancelBet_NonCreatorUser_BetRemainsUnchanged()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db);
        var otherUser = await SeedUserAsync(db);
        var market = await SeedMarketAsync(db);
        var bet = await SeedBetAsync(db, creator.Id, market.Id);
        var svc = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CancelBetAsync(otherUser.Id, bet.Id));

        var unchanged = await db.Bets.FindAsync(bet.Id);
        Assert.Equal(BetStatus.Pending, unchanged!.Status);
    }

    // ── CANNOT_CANCEL_ACTIVE_BET rejection ───────────────────────────────────

    [Theory]
    [InlineData(BetStatus.Active)]
    [InlineData(BetStatus.Settled)]
    [InlineData(BetStatus.Voided)]
    public async Task CancelBet_NonPendingBet_ThrowsCannotCancelActiveBet(BetStatus status)
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db);
        var market = await SeedMarketAsync(db);
        var bet = await SeedBetAsync(db, creator.Id, market.Id, status: status);
        var svc = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CancelBetAsync(creator.Id, bet.Id));

        Assert.Equal("CANNOT_CANCEL_ACTIVE_BET", ex.Message);
    }

    [Fact]
    public async Task CancelBet_ActiveBet_BalanceUnchanged()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db, 900m);
        creator.ReservedBalance = 100m;
        await db.SaveChangesAsync();

        var market = await SeedMarketAsync(db);
        var bet = await SeedBetAsync(db, creator.Id, market.Id, 100m, BetStatus.Active);
        var svc = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CancelBetAsync(creator.Id, bet.Id));

        var unchanged = await db.Users.FindAsync(creator.Id);
        Assert.Equal(900m, unchanged!.VirtualBalance);
        Assert.Equal(100m, unchanged.ReservedBalance);
    }

    // ── Bet not found ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelBet_UnknownBet_ThrowsKeyNotFoundException()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db);
        var svc = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.CancelBetAsync(user.Id, Guid.NewGuid()));
    }
}
