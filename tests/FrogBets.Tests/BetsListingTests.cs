using FrogBets.Api.Services;
using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FrogBets.Tests;

/// <summary>
/// Unit tests for BetService listing methods:
///   GetUserBetsAsync  — Requirements 8.1, 8.2, 8.3
///   GetMarketplaceBetsAsync — Requirement 8.4
/// </summary>
public class BetsListingTests
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

    private static BetService CreateService(FrogBetsDbContext db)
        => new BetService(db, new BalanceService(db));

    private static async Task<User> SeedUserAsync(FrogBetsDbContext db, decimal balance = 1000m)
    {
        var user = new User
        {
            Id             = Guid.NewGuid(),
            Username       = Guid.NewGuid().ToString("N"),
            PasswordHash   = "hash",
            VirtualBalance = balance,
            CreatedAt      = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Market> SeedMarketAsync(FrogBetsDbContext db,
        MarketType type = MarketType.MapWinner,
        int? mapNumber = 1)
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
            Type      = type,
            MapNumber = mapNumber,
            Status    = MarketStatus.Open,
            Game      = game,
        };
        game.Markets.Add(market);
        db.Games.Add(game);
        await db.SaveChangesAsync();
        return market;
    }

    private static async Task<Bet> SeedBetAsync(FrogBetsDbContext db,
        Guid creatorId,
        Market market,
        BetStatus status = BetStatus.Pending,
        Guid? coveredById = null,
        string creatorOption = "TeamA",
        string? covererOption = null,
        DateTime? settledAt = null,
        DateTime? coveredAt = null)
    {
        var bet = new Bet
        {
            Id            = Guid.NewGuid(),
            MarketId      = market.Id,
            Market        = market,
            CreatorId     = creatorId,
            CoveredById   = coveredById,
            CreatorOption = creatorOption,
            CovererOption = covererOption,
            Amount        = 100m,
            Status        = status,
            CreatedAt     = DateTime.UtcNow,
            CoveredAt     = coveredAt,
            SettledAt     = settledAt,
        };
        db.Bets.Add(bet);
        await db.SaveChangesAsync();
        return bet;
    }

    // ── GetUserBetsAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserBets_ReturnsOnlyUserBets()
    {
        await using var db = CreateDb();
        var user  = await SeedUserAsync(db);
        var other = await SeedUserAsync(db);
        var market = await SeedMarketAsync(db);

        await SeedBetAsync(db, user.Id, market);
        await SeedBetAsync(db, other.Id, market);

        var svc  = CreateService(db);
        var bets = await svc.GetUserBetsAsync(user.Id);

        Assert.Single(bets);
        Assert.NotEqual(Guid.Empty, bets[0].Id);
    }

    [Fact]
    public async Task GetUserBets_IncludesBetsWhereUserIsCoverer()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db);
        var coverer = await SeedUserAsync(db);
        var market  = await SeedMarketAsync(db);

        await SeedBetAsync(db, creator.Id, market,
            status: BetStatus.Active,
            coveredById: coverer.Id,
            covererOption: "TeamB",
            coveredAt: DateTime.UtcNow);

        var svc  = CreateService(db);
        var bets = await svc.GetUserBetsAsync(coverer.Id);

        Assert.Single(bets);
        Assert.Equal(coverer.Id, bets[0].CoveredById);
    }

    [Fact]
    public async Task GetUserBets_IncludesPendingActiveAndSettled()
    {
        await using var db = CreateDb();
        var user   = await SeedUserAsync(db);
        var market = await SeedMarketAsync(db);

        await SeedBetAsync(db, user.Id, market, BetStatus.Pending);
        var m2 = await SeedMarketAsync(db);
        await SeedBetAsync(db, user.Id, m2, BetStatus.Active);
        var m3 = await SeedMarketAsync(db);
        await SeedBetAsync(db, user.Id, m3, BetStatus.Settled, settledAt: DateTime.UtcNow);

        var svc  = CreateService(db);
        var bets = await svc.GetUserBetsAsync(user.Id);

        Assert.Equal(3, bets.Count);
        Assert.Contains(bets, b => b.Status == BetStatus.Pending);
        Assert.Contains(bets, b => b.Status == BetStatus.Active);
        Assert.Contains(bets, b => b.Status == BetStatus.Settled);
    }

    [Fact]
    public async Task GetUserBets_DoesNotReturnCancelledBets_OfOtherUsers()
    {
        // Cancelled bets of the user ARE returned (no filter on status for user's own bets)
        // but cancelled bets of other users are not
        await using var db = CreateDb();
        var user  = await SeedUserAsync(db);
        var other = await SeedUserAsync(db);
        var market = await SeedMarketAsync(db);

        await SeedBetAsync(db, other.Id, market, BetStatus.Cancelled);

        var svc  = CreateService(db);
        var bets = await svc.GetUserBetsAsync(user.Id);

        Assert.Empty(bets);
    }

    [Fact]
    public async Task GetUserBets_BetDtoContainsRequiredFields()
    {
        await using var db = CreateDb();
        var user   = await SeedUserAsync(db);
        var market = await SeedMarketAsync(db, MarketType.MapWinner, mapNumber: 2);

        await SeedBetAsync(db, user.Id, market, creatorOption: "TeamA");

        var svc  = CreateService(db);
        var bets = await svc.GetUserBetsAsync(user.Id);

        var dto = Assert.Single(bets);
        Assert.Equal(market.Id,       dto.MarketId);
        Assert.Equal(MarketType.MapWinner, dto.MarketType);
        Assert.Equal(2,               dto.MapNumber);
        Assert.Equal(market.GameId,   dto.GameId);
        Assert.Equal("TeamA",         dto.CreatorOption);
        Assert.Null(dto.CovererOption);
        Assert.Equal(100m,            dto.Amount);
        Assert.Equal(BetStatus.Pending, dto.Status);
        Assert.Null(dto.CoveredById);
    }

    [Fact]
    public async Task GetUserBets_ActiveBetContainsCovererInfo()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db);
        var coverer = await SeedUserAsync(db);
        var market  = await SeedMarketAsync(db);

        await SeedBetAsync(db, creator.Id, market,
            status: BetStatus.Active,
            coveredById: coverer.Id,
            covererOption: "TeamB",
            coveredAt: DateTime.UtcNow);

        var svc  = CreateService(db);
        var bets = await svc.GetUserBetsAsync(creator.Id);

        var dto = Assert.Single(bets);
        Assert.Equal(coverer.Id, dto.CoveredById);
        Assert.Equal("TeamB",    dto.CovererOption);
    }

    // ── Settled ordering (Requirement 8.3) ────────────────────────────────────

    [Fact]
    public async Task GetUserBets_SettledBetsOrderedBySettledAtDescending()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db);

        var m1 = await SeedMarketAsync(db);
        var m2 = await SeedMarketAsync(db);
        var m3 = await SeedMarketAsync(db);

        var t1 = DateTime.UtcNow.AddHours(-3);
        var t2 = DateTime.UtcNow.AddHours(-1);
        var t3 = DateTime.UtcNow.AddHours(-2);

        var bet1 = await SeedBetAsync(db, user.Id, m1, BetStatus.Settled, settledAt: t1);
        var bet2 = await SeedBetAsync(db, user.Id, m2, BetStatus.Settled, settledAt: t2);
        var bet3 = await SeedBetAsync(db, user.Id, m3, BetStatus.Settled, settledAt: t3);

        var svc  = CreateService(db);
        var bets = await svc.GetUserBetsAsync(user.Id);

        // Settled bets should come first, ordered by SettledAt desc: bet2 (t2), bet3 (t3), bet1 (t1)
        var settledBets = bets.Where(b => b.Status == BetStatus.Settled).ToList();
        Assert.Equal(3, settledBets.Count);
        Assert.Equal(bet2.Id, settledBets[0].Id);
        Assert.Equal(bet3.Id, settledBets[1].Id);
        Assert.Equal(bet1.Id, settledBets[2].Id);
    }

    // ── GetMarketplaceBetsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetMarketplace_ReturnsOnlyPendingBets()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db);
        var viewer  = await SeedUserAsync(db);
        var market  = await SeedMarketAsync(db);

        await SeedBetAsync(db, creator.Id, market, BetStatus.Pending);
        var m2 = await SeedMarketAsync(db);
        await SeedBetAsync(db, creator.Id, m2, BetStatus.Active);

        var svc  = CreateService(db);
        var bets = await svc.GetMarketplaceBetsAsync(viewer.Id);

        Assert.Single(bets);
        Assert.Equal(BetStatus.Pending, bets[0].Status);
    }

    [Fact]
    public async Task GetMarketplace_ExcludesOwnBets()
    {
        await using var db = CreateDb();
        var user   = await SeedUserAsync(db);
        var other  = await SeedUserAsync(db);
        var market = await SeedMarketAsync(db);

        await SeedBetAsync(db, user.Id,  market, BetStatus.Pending);
        var m2 = await SeedMarketAsync(db);
        await SeedBetAsync(db, other.Id, m2, BetStatus.Pending);

        var svc  = CreateService(db);
        var bets = await svc.GetMarketplaceBetsAsync(user.Id);

        Assert.Single(bets);
        Assert.NotEqual(user.Id, bets[0].Id); // the returned bet is from 'other'
    }

    [Fact]
    public async Task GetMarketplace_EmptyWhenNoPendingBetsFromOthers()
    {
        await using var db = CreateDb();
        var user   = await SeedUserAsync(db);
        var market = await SeedMarketAsync(db);

        await SeedBetAsync(db, user.Id, market, BetStatus.Pending);

        var svc  = CreateService(db);
        var bets = await svc.GetMarketplaceBetsAsync(user.Id);

        Assert.Empty(bets);
    }

    [Fact]
    public async Task GetMarketplace_BetDtoContainsMarketInfo()
    {
        await using var db = CreateDb();
        var creator = await SeedUserAsync(db);
        var viewer  = await SeedUserAsync(db);
        var market  = await SeedMarketAsync(db, MarketType.SeriesWinner, mapNumber: null);

        await SeedBetAsync(db, creator.Id, market, creatorOption: "TeamA");

        var svc  = CreateService(db);
        var bets = await svc.GetMarketplaceBetsAsync(viewer.Id);

        var dto = Assert.Single(bets);
        Assert.Equal(market.Id,            dto.MarketId);
        Assert.Equal(MarketType.SeriesWinner, dto.MarketType);
        Assert.Null(dto.MapNumber);
        Assert.Equal(market.GameId,        dto.GameId);
        Assert.Equal("TeamA",              dto.CreatorOption);
        Assert.Null(dto.CovererOption);
        Assert.Equal(BetStatus.Pending,    dto.Status);
        Assert.Null(dto.CoveredById);
    }
}
