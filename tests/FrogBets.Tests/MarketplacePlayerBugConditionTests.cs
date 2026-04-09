using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;
using FrogBets.Infrastructure.Data;
using FrogBets.Tests.Integration;

namespace FrogBets.Tests;

/// <summary>
/// Tests for GET /api/games/{id}/players — endpoint now returns Users by TeamId.
///
/// After the register-result-players-dropdown feature, the endpoint queries Users
/// (not CS2Players) whose TeamId matches one of the game's teams.
/// </summary>
public class MarketplacePlayerBugConditionTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public MarketplacePlayerBugConditionTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private void AuthAs(Guid userId, string username, bool isAdmin = false)
    {
        var token = IntegrationTestFactory.GenerateToken(userId, username, isAdmin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Endpoint returns all Users whose TeamId belongs to the game's teams.
    /// </summary>
    [Fact]
    public async Task GetGamePlayers_WithUsersInTeams_ReturnsAllFourPlayers()
    {
        using var db = _factory.CreateDbContext();

        var authUser = await IntegrationTestFactory.SeedUserAsync(db, "auth_user_bugcond");
        AuthAs(authUser.Id, authUser.Username);

        var teamA = new CS2Team { Id = Guid.NewGuid(), Name = "TeamAlpha_v2", CreatedAt = DateTime.UtcNow };
        var teamB = new CS2Team { Id = Guid.NewGuid(), Name = "TeamBeta_v2", CreatedAt = DateTime.UtcNow };
        db.CS2Teams.AddRange(teamA, teamB);

        var game = new Game
        {
            Id           = Guid.NewGuid(),
            TeamA        = teamA.Name,
            TeamB        = teamB.Name,
            ScheduledAt  = DateTime.UtcNow.AddDays(1),
            NumberOfMaps = 3,
            Status       = GameStatus.Scheduled,
            CreatedAt    = DateTime.UtcNow,
        };
        db.Games.Add(game);

        // 2 users in TeamA, 2 users in TeamB
        var userA1 = new User { Id = Guid.NewGuid(), Username = "player_a1_user", PasswordHash = "hash", VirtualBalance = 1000m, ReservedBalance = 0m, TeamId = teamA.Id, CreatedAt = DateTime.UtcNow };
        var userA2 = new User { Id = Guid.NewGuid(), Username = "player_a2_user", PasswordHash = "hash", VirtualBalance = 1000m, ReservedBalance = 0m, TeamId = teamA.Id, CreatedAt = DateTime.UtcNow };
        var userB1 = new User { Id = Guid.NewGuid(), Username = "player_b1_user", PasswordHash = "hash", VirtualBalance = 1000m, ReservedBalance = 0m, TeamId = teamB.Id, CreatedAt = DateTime.UtcNow };
        var userB2 = new User { Id = Guid.NewGuid(), Username = "player_b2_user", PasswordHash = "hash", VirtualBalance = 1000m, ReservedBalance = 0m, TeamId = teamB.Id, CreatedAt = DateTime.UtcNow };
        db.Users.AddRange(userA1, userA2, userB1, userB2);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/api/games/{game.Id}/players");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("player_a1_user", body);
        Assert.Contains("player_a2_user", body);
        Assert.Contains("player_b1_user", body);
        Assert.Contains("player_b2_user", body);
    }

    /// <summary>
    /// Endpoint returns exactly 4 users when 2 users per team are in the game's teams.
    /// </summary>
    [Fact]
    public async Task GetGamePlayers_WithUsersInTeams_CountIsExactlyFour()
    {
        using var db = _factory.CreateDbContext();

        var authUser = await IntegrationTestFactory.SeedUserAsync(db, "auth_user_count_v2");
        AuthAs(authUser.Id, authUser.Username);

        var teamA = new CS2Team { Id = Guid.NewGuid(), Name = "CountTeamA_v2", CreatedAt = DateTime.UtcNow };
        var teamB = new CS2Team { Id = Guid.NewGuid(), Name = "CountTeamB_v2", CreatedAt = DateTime.UtcNow };
        db.CS2Teams.AddRange(teamA, teamB);

        var game = new Game
        {
            Id           = Guid.NewGuid(),
            TeamA        = teamA.Name,
            TeamB        = teamB.Name,
            ScheduledAt  = DateTime.UtcNow.AddDays(1),
            NumberOfMaps = 1,
            Status       = GameStatus.Scheduled,
            CreatedAt    = DateTime.UtcNow,
        };
        db.Games.Add(game);

        db.Users.AddRange(
            new User { Id = Guid.NewGuid(), Username = "cnt_ua1", PasswordHash = "h", VirtualBalance = 100m, ReservedBalance = 0m, TeamId = teamA.Id, CreatedAt = DateTime.UtcNow },
            new User { Id = Guid.NewGuid(), Username = "cnt_ua2", PasswordHash = "h", VirtualBalance = 100m, ReservedBalance = 0m, TeamId = teamA.Id, CreatedAt = DateTime.UtcNow },
            new User { Id = Guid.NewGuid(), Username = "cnt_ub1", PasswordHash = "h", VirtualBalance = 100m, ReservedBalance = 0m, TeamId = teamB.Id, CreatedAt = DateTime.UtcNow },
            new User { Id = Guid.NewGuid(), Username = "cnt_ub2", PasswordHash = "h", VirtualBalance = 100m, ReservedBalance = 0m, TeamId = teamB.Id, CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/api/games/{game.Id}/players");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var players = await response.Content.ReadFromJsonAsync<List<PlayerDto>>();
        Assert.NotNull(players);
        Assert.Equal(4, players!.Count);
    }

    private record PlayerDto(Guid id, string username, string teamName);
}
