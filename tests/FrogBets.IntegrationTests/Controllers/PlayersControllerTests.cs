using System.Net;
using System.Net.Http.Json;
using FrogBets.IntegrationTests.Helpers;
using Xunit;

namespace FrogBets.IntegrationTests.Controllers;

public class PlayersControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PlayersControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── POST /api/players ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePlayer_AsAdmin_Returns201()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var admin  = await SeedHelper.SeedUserAsync(db, isAdmin: true);
        var team   = await SeedHelper.SeedTeamAsync(db);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, admin.Id, admin.Username, isAdmin: true);

        // Act
        var res = await client.PostAsJsonAsync("/api/players", new
        {
            nickname = "player_" + Guid.NewGuid().ToString("N")[..6],
            teamId   = team.Id,
        });

        // Assert
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task CreatePlayer_AsNonAdmin_Returns403()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user   = await SeedHelper.SeedUserAsync(db);
        var team   = await SeedHelper.SeedTeamAsync(db);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username, isAdmin: false);

        // Act
        var res = await client.PostAsJsonAsync("/api/players", new
        {
            nickname = "player_x",
            teamId   = team.Id,
        });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ── GET /api/players ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetPlayers_AsAdmin_Returns200()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var admin  = await SeedHelper.SeedUserAsync(db, isAdmin: true);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, admin.Id, admin.Username, isAdmin: true);

        // Act
        var res = await client.GetAsync("/api/players");

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetPlayers_AsNonAdmin_Returns403()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user   = await SeedHelper.SeedUserAsync(db);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username, isAdmin: false);

        // Act
        var res = await client.GetAsync("/api/players");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ── GET /api/players/ranking ──────────────────────────────────────────────

    [Fact]
    public async Task GetRanking_IsPublic_Returns200()
    {
        var res = await _factory.CreateClient().GetAsync("/api/players/ranking");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetRanking_ReturnsOrderedList()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var team    = await SeedHelper.SeedTeamAsync(db);
        var player1 = await SeedHelper.SeedPlayerAsync(db, team.Id, "topplayer");
        var player2 = await SeedHelper.SeedPlayerAsync(db, team.Id, "lowplayer");

        player1.PlayerScore  = 1.5;
        player1.MatchesCount = 10;
        player2.PlayerScore  = 0.8;
        player2.MatchesCount = 5;
        await db.SaveChangesAsync();

        // Act
        var res = await _factory.CreateClient().GetAsync("/api/players/ranking");
        var body = await res.Content.ReadFromJsonAsync<List<RankingItem>>();

        // Assert
        Assert.NotNull(body);
        Assert.True(body![0].playerScore >= body[1].playerScore);
    }

    // ── POST /api/players/:id/stats ───────────────────────────────────────────

    [Fact]
    public async Task RegisterStats_AsAdmin_Returns201()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var admin  = await SeedHelper.SeedUserAsync(db, isAdmin: true);
        var team   = await SeedHelper.SeedTeamAsync(db);
        var player = await SeedHelper.SeedPlayerAsync(db, team.Id);
        var (game, _) = await SeedHelper.SeedGameWithMarketAsync(db);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, admin.Id, admin.Username, isAdmin: true);

        // Act
        var res = await client.PostAsJsonAsync($"/api/players/{player.Id}/stats", new
        {
            gameId      = game.Id,
            kills       = 20,
            deaths      = 10,
            assists     = 5,
            totalDamage = 2500.0,
            rounds      = 30,
            kastPercent = 75.0,
        });

        // Assert
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private record RankingItem(int position, string playerId, string nickname, string teamName, double playerScore, int matchesCount);
}
