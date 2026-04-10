using FrogBets.Api.Services;
using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FrogBets.Tests;

/// <summary>
/// Unit tests for BetService.CreateBetAsync.
/// </summary>
public class BetServiceCreateTests
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
        GameStatus gameStatus = GameStatus.Scheduled,
        MarketStatus marketStatus = MarketStatus.Open)
    {
        var game = new Game
        {
            Id           = Guid.NewGuid(),
            TeamA        = "Team A",
            TeamB        = "Team B",
            ScheduledAt  = DateTime.UtcNow.AddDays(1),
            NumberOfMaps = 1,
            Status       = gameStatus,
            CreatedAt    = DateTime.UtcNow,
        };

        var market = new Market
        {
            Id        = Guid.NewGuid(),
            GameId    = game.Id,
            Type      = MarketType.MapWinner,
            MapNumber = 1,
            Status    = marketStatus,
            Game      = game,
        };

        game.Markets.Add(market);
        db.Games.Add(game);
        await db.SaveChangesAsync();

        return (game, market);
    }

    private static BetService CreateService(FrogBetsDbContext db)
        => new BetService(db, new BalanceService(db));

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBet_ValidInputs_ReturnsBetId()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, 500m);
        var (_, market) = await SeedGameWithMarketAsync(db);
        var svc = CreateService(db);

        var betId = await svc.CreateBetAsync(user.Id, market.Id, "Team A", 100m);

        Assert.NotEqual(Guid.Empty, betId);
    }

    [Fact]
    public async Task CreateBet_ValidInputs_BetSavedWithPendingStatus()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, 500m);
        var (_, market) = await SeedGameWithMarketAsync(db);
        var svc = CreateService(db);

        var betId = await svc.CreateBetAsync(user.Id, market.Id, "Team A", 100m);

        var bet = await db.Bets.FindAsync(betId);
        Assert.NotNull(bet);
        Assert.Equal(BetStatus.Pending, bet.Status);
        Assert.Equal(user.Id, bet.CreatorId);
        Assert.Equal(market.Id, bet.MarketId);
        Assert.Equal("Team A", bet.CreatorOption);
        Assert.Equal(100m, bet.Amount);
    }

    [Fact]
    public async Task CreateBet_ValidInputs_ReservesBalance()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, 500m);
        var (_, market) = await SeedGameWithMarketAsync(db);
        var svc = CreateService(db);

        await svc.CreateBetAsync(user.Id, market.Id, "Team A", 200m);

        var updated = await db.Users.FindAsync(user.Id);
        Assert.Equal(300m, updated!.VirtualBalance);
        Assert.Equal(200m, updated.ReservedBalance);
    }

    // ── Market not open ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(MarketStatus.Closed)]
    [InlineData(MarketStatus.Settled)]
    [InlineData(MarketStatus.Voided)]
    public async Task CreateBet_MarketNotOpen_ThrowsMarketNotOpen(MarketStatus status)
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db);
        var (_, market) = await SeedGameWithMarketAsync(db, marketStatus: status);
        var svc = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateBetAsync(user.Id, market.Id, "Team A", 100m));

        Assert.Equal("MARKET_NOT_OPEN", ex.Message);
    }

    // ── Game already started ──────────────────────────────────────────────────

    [Theory]
    [InlineData(GameStatus.InProgress)]
    [InlineData(GameStatus.Finished)]
    public async Task CreateBet_GameNotScheduled_ThrowsGameAlreadyStarted(GameStatus status)
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db);
        var (_, market) = await SeedGameWithMarketAsync(db, gameStatus: status);
        var svc = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateBetAsync(user.Id, market.Id, "Team A", 100m));

        Assert.Equal("GAME_ALREADY_STARTED", ex.Message);
    }

    // ── Duplicate bet ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(BetStatus.Pending)]
    [InlineData(BetStatus.Active)]
    public async Task CreateBet_DuplicateBetOnMarket_ThrowsDuplicateBetOnMarket(BetStatus existingStatus)
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, 1000m);
        var (_, market) = await SeedGameWithMarketAsync(db);

        // Seed an existing bet
        db.Bets.Add(new Bet
        {
            Id            = Guid.NewGuid(),
            MarketId      = market.Id,
            CreatorId     = user.Id,
            CreatorOption = "Team A",
            Amount        = 100m,
            Status        = existingStatus,
            CreatedAt     = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateBetAsync(user.Id, market.Id, "Team B", 50m));

        Assert.Equal("DUPLICATE_BET_ON_MARKET", ex.Message);
    }

    [Fact]
    public async Task CreateBet_DifferentUserSameMarket_Succeeds()
    {
        await using var db = CreateDb();
        var user1 = await SeedUserAsync(db, 500m);
        var user2 = await SeedUserAsync(db, 500m);
        var (_, market) = await SeedGameWithMarketAsync(db);

        db.Bets.Add(new Bet
        {
            Id            = Guid.NewGuid(),
            MarketId      = market.Id,
            CreatorId     = user1.Id,
            CreatorOption = "Team A",
            Amount        = 100m,
            Status        = BetStatus.Pending,
            CreatedAt     = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);

        // user2 can still bet on the same market
        var betId = await svc.CreateBetAsync(user2.Id, market.Id, "Team B", 100m);
        Assert.NotEqual(Guid.Empty, betId);
    }

    [Fact]
    public async Task CreateBet_SameUserSettledBetOnMarket_Succeeds()
    {
        // A settled/cancelled bet should not block a new one
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, 500m);
        var (_, market) = await SeedGameWithMarketAsync(db);

        db.Bets.Add(new Bet
        {
            Id            = Guid.NewGuid(),
            MarketId      = market.Id,
            CreatorId     = user.Id,
            CreatorOption = "Team A",
            Amount        = 100m,
            Status        = BetStatus.Settled,
            CreatedAt     = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);

        var betId = await svc.CreateBetAsync(user.Id, market.Id, "Team A", 50m);
        Assert.NotEqual(Guid.Empty, betId);
    }

    // ── Insufficient balance ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateBet_InsufficientBalance_ThrowsInsufficientBalance()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, virtualBalance: 50m);
        var (_, market) = await SeedGameWithMarketAsync(db);
        var svc = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateBetAsync(user.Id, market.Id, "Team A", 100m));

        Assert.Equal("INSUFFICIENT_BALANCE", ex.Message);
    }

    [Fact]
    public async Task CreateBet_InsufficientBalance_DoesNotSaveBet()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, virtualBalance: 50m);
        var (_, market) = await SeedGameWithMarketAsync(db);
        var svc = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateBetAsync(user.Id, market.Id, "Team A", 100m));

        Assert.Empty(db.Bets);
    }

    // ── Market not found ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBet_UnknownMarket_ThrowsKeyNotFoundException()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db);
        var svc = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.CreateBetAsync(user.Id, Guid.NewGuid(), "Team A", 100m));
    }

    // ── Option validation — player-based markets ──────────────────────────────

    private static async Task<(Game game, Market market, CS2Player player)> SeedPlayerMarketAsync(
        FrogBetsDbContext db,
        MarketType marketType = MarketType.TopKills)
    {
        var team = new CS2Team
        {
            Id        = Guid.NewGuid(),
            Name      = "TestTeam",
            CreatedAt = DateTime.UtcNow,
        };

        var player = new CS2Player
        {
            Id        = Guid.NewGuid(),
            TeamId    = team.Id,
            Nickname  = "sniper99",
            CreatedAt = DateTime.UtcNow,
        };
        team.Players.Add(player);
        db.CS2Teams.Add(team);

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
            MapNumber = 1,
            Status    = MarketStatus.Open,
            Game      = game,
        };

        game.Markets.Add(market);
        db.Games.Add(game);
        await db.SaveChangesAsync();

        return (game, market, player);
    }

    [Theory]
    [InlineData(MarketType.TopKills)]
    [InlineData(MarketType.MostDeaths)]
    [InlineData(MarketType.MostUtilityDamage)]
    public async Task CreateBet_PlayerMarket_PlainPlayerName_Succeeds(MarketType marketType)
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, 500m);
        var (_, market, player) = await SeedPlayerMarketAsync(db, marketType);
        var svc = CreateService(db);

        var betId = await svc.CreateBetAsync(user.Id, market.Id, player.Nickname, 100m);

        Assert.NotEqual(Guid.Empty, betId);
    }

    [Theory]
    [InlineData(MarketType.TopKills)]
    [InlineData(MarketType.MostDeaths)]
    [InlineData(MarketType.MostUtilityDamage)]
    public async Task CreateBet_PlayerMarket_NotPrefixedOption_Succeeds(MarketType marketType)
    {
        // Regression test for H-1: NOT_-prefixed options must be accepted for player markets.
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, 500m);
        var (_, market, player) = await SeedPlayerMarketAsync(db, marketType);
        var svc = CreateService(db);

        var betId = await svc.CreateBetAsync(user.Id, market.Id, "NOT_" + player.Nickname, 100m);

        Assert.NotEqual(Guid.Empty, betId);
    }

    [Fact]
    public async Task CreateBet_PlayerMarket_UnknownPlayer_ThrowsInvalidBetOption()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, 500m);
        var (_, market, _) = await SeedPlayerMarketAsync(db);
        var svc = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateBetAsync(user.Id, market.Id, "nobody", 100m));

        Assert.Equal("INVALID_BET_OPTION", ex.Message);
    }

    [Fact]
    public async Task CreateBet_PlayerMarket_NotPrefixedUnknownPlayer_ThrowsInvalidBetOption()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, 500m);
        var (_, market, _) = await SeedPlayerMarketAsync(db);
        var svc = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateBetAsync(user.Id, market.Id, "NOT_nobody", 100m));

        Assert.Equal("INVALID_BET_OPTION", ex.Message);
    }

    [Fact]
    public async Task CreateBet_PlayerMarket_EmptyOption_ThrowsInvalidBetOption()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, 500m);
        var (_, market, _) = await SeedPlayerMarketAsync(db);
        var svc = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateBetAsync(user.Id, market.Id, "", 100m));

        Assert.Equal("INVALID_BET_OPTION", ex.Message);
    }

    [Theory]
    [InlineData("TeamA")]
    [InlineData("TeamB")]
    public async Task CreateBet_TeamWinnerMarket_ValidTeamOption_Succeeds(string option)
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, 500m);
        var (_, market) = await SeedGameWithMarketAsync(db, marketStatus: MarketStatus.Open);
        var svc = CreateService(db);

        var betId = await svc.CreateBetAsync(user.Id, market.Id, option, 100m);

        Assert.NotEqual(Guid.Empty, betId);
    }

    [Fact]
    public async Task CreateBet_TeamWinnerMarket_InvalidOption_ThrowsInvalidBetOption()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, 500m);
        var (_, market) = await SeedGameWithMarketAsync(db, marketStatus: MarketStatus.Open);
        var svc = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateBetAsync(user.Id, market.Id, "TeamC", 100m));

        Assert.Equal("INVALID_BET_OPTION", ex.Message);
    }
}
