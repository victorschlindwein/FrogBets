using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;
using FrogBets.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace FrogBets.IntegrationTests.Helpers;

public static class SeedHelper
{
    public static FrogBetsDbContext GetDb(TestWebApplicationFactory factory)
    {
        var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<FrogBetsDbContext>();
    }

    public static async Task<User> SeedUserAsync(
        FrogBetsDbContext db,
        string? username = null,
        bool isAdmin = false,
        bool isTeamLeader = false,
        decimal virtualBalance = 1000m,
        Guid? teamId = null)
    {
        var user = new User
        {
            Id             = Guid.NewGuid(),
            Username       = username ?? Guid.NewGuid().ToString("N")[..12],
            PasswordHash   = BCrypt.Net.BCrypt.HashPassword("password123"),
            IsAdmin        = isAdmin,
            IsTeamLeader   = isTeamLeader,
            VirtualBalance = virtualBalance,
            TeamId         = teamId,
            CreatedAt      = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public static async Task<CS2Team> SeedTeamAsync(FrogBetsDbContext db, string? name = null)
    {
        var team = new CS2Team
        {
            Id        = Guid.NewGuid(),
            Name      = name ?? Guid.NewGuid().ToString("N")[..10],
            CreatedAt = DateTime.UtcNow,
        };
        db.CS2Teams.Add(team);
        await db.SaveChangesAsync();
        return team;
    }

    public static async Task<(Game game, Market market)> SeedGameWithMarketAsync(
        FrogBetsDbContext db,
        GameStatus gameStatus = GameStatus.Scheduled,
        MarketStatus marketStatus = MarketStatus.Open)
    {
        var game = new Game
        {
            Id           = Guid.NewGuid(),
            TeamA        = "FURIA",
            TeamB        = "NAVI",
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

    public static async Task<Invite> SeedInviteAsync(
        FrogBetsDbContext db,
        DateTime? expiresAt = null,
        bool used = false,
        Guid? usedByUserId = null)
    {
        var invite = new Invite
        {
            Id          = Guid.NewGuid(),
            Token       = Guid.NewGuid().ToString("N"),
            Description = "Test invite",
            ExpiresAt   = expiresAt ?? DateTime.UtcNow.AddDays(7),
            CreatedAt   = DateTime.UtcNow,
            UsedAt      = used ? DateTime.UtcNow : null,
            UsedByUserId = used ? usedByUserId : null,
        };
        db.Invites.Add(invite);
        await db.SaveChangesAsync();
        return invite;
    }

    public static async Task<Bet> SeedBetAsync(
        FrogBetsDbContext db,
        Guid marketId,
        Guid creatorId,
        BetStatus status = BetStatus.Pending,
        decimal amount = 100m)
    {
        var bet = new Bet
        {
            Id            = Guid.NewGuid(),
            MarketId      = marketId,
            CreatorId     = creatorId,
            CreatorOption = "FURIA",
            Amount        = amount,
            Status        = status,
            CreatedAt     = DateTime.UtcNow,
        };
        db.Bets.Add(bet);
        await db.SaveChangesAsync();
        return bet;
    }

    public static async Task<CS2Player> SeedPlayerAsync(FrogBetsDbContext db, Guid teamId, string? nickname = null)
    {
        var player = new CS2Player
        {
            Id        = Guid.NewGuid(),
            TeamId    = teamId,
            Nickname  = nickname ?? Guid.NewGuid().ToString("N")[..8],
            CreatedAt = DateTime.UtcNow,
        };
        db.CS2Players.Add(player);
        await db.SaveChangesAsync();
        return player;
    }
}
