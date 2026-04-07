using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;

namespace FrogBets.Tests.Integration;

public class GamesIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public GamesIntegrationTests(IntegrationTestFactory factory)
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

    // ── GET /api/games ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetGames_Anonymous_Returns200()
    {
        var res = await _client.GetAsync("/api/games");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetGames_ReturnsSeededGames()
    {
        using var db = _factory.CreateDbContext();
        var game = new Game
        {
            Id           = Guid.NewGuid(),
            TeamA        = "TeamAlpha",
            TeamB        = "TeamBeta",
            ScheduledAt  = DateTime.UtcNow.AddDays(1),
            NumberOfMaps = 3,
            Status       = GameStatus.Scheduled,
            CreatedAt    = DateTime.UtcNow,
        };
        db.Games.Add(game);
        await db.SaveChangesAsync();

        var res = await _client.GetAsync("/api/games");
        var body = await res.Content.ReadAsStringAsync();

        Assert.Contains("TeamAlpha", body);
        Assert.Contains("TeamBeta", body);
    }

    // ── GET /api/games/:id ────────────────────────────────────────────────────

    [Fact]
    public async Task GetGameById_ExistingGame_Returns200WithMarkets()
    {
        using var db = _factory.CreateDbContext();
        var game = new Game
        {
            Id           = Guid.NewGuid(),
            TeamA        = "TeamX",
            TeamB        = "TeamY",
            ScheduledAt  = DateTime.UtcNow.AddDays(2),
            NumberOfMaps = 1,
            Status       = GameStatus.Scheduled,
            CreatedAt    = DateTime.UtcNow,
        };
        game.Markets.Add(new Market
        {
            Id        = Guid.NewGuid(),
            GameId    = game.Id,
            Type      = MarketType.MapWinner,
            MapNumber = 1,
            Status    = MarketStatus.Open,
        });
        db.Games.Add(game);
        await db.SaveChangesAsync();

        var res = await _client.GetAsync($"/api/games/{game.Id}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("TeamX", body);
        Assert.Contains("MapWinner", body);
    }

    [Fact]
    public async Task GetGameById_NonExistent_Returns404()
    {
        var res = await _client.GetAsync($"/api/games/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ── POST /api/games ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateGame_AsAdmin_Returns201()
    {
        using var db = _factory.CreateDbContext();
        var admin = await IntegrationTestFactory.SeedUserAsync(db, "admin_games", isAdmin: true);
        AuthAs(admin.Id, admin.Username, isAdmin: true);

        var res = await _client.PostAsJsonAsync("/api/games", new
        {
            teamA        = "FrogTeam",
            teamB        = "RivalTeam",
            scheduledAt  = DateTime.UtcNow.AddDays(3).ToString("o"),
            numberOfMaps = 3,
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task CreateGame_AsNonAdmin_Returns403()
    {
        using var db = _factory.CreateDbContext();
        var user = await IntegrationTestFactory.SeedUserAsync(db, "nonadmin_games");
        AuthAs(user.Id, user.Username, isAdmin: false);

        var res = await _client.PostAsJsonAsync("/api/games", new
        {
            teamA        = "FrogTeam",
            teamB        = "RivalTeam",
            scheduledAt  = DateTime.UtcNow.AddDays(3).ToString("o"),
            numberOfMaps = 1,
        });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task CreateGame_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var res = await _client.PostAsJsonAsync("/api/games", new
        {
            teamA        = "A",
            teamB        = "B",
            scheduledAt  = DateTime.UtcNow.AddDays(1).ToString("o"),
            numberOfMaps = 1,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ── PATCH /api/games/:id/start ────────────────────────────────────────────

    [Fact]
    public async Task StartGame_AsAdmin_Returns204()
    {
        using var db = _factory.CreateDbContext();
        var admin = await IntegrationTestFactory.SeedUserAsync(db, "admin_start", isAdmin: true);
        var game = new Game
        {
            Id           = Guid.NewGuid(),
            TeamA        = "A",
            TeamB        = "B",
            ScheduledAt  = DateTime.UtcNow.AddDays(1),
            NumberOfMaps = 1,
            Status       = GameStatus.Scheduled,
            CreatedAt    = DateTime.UtcNow,
        };
        db.Games.Add(game);
        await db.SaveChangesAsync();

        AuthAs(admin.Id, admin.Username, isAdmin: true);
        var res = await _client.PatchAsync($"/api/games/{game.Id}/start", null);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task StartGame_NonExistent_Returns404()
    {
        using var db = _factory.CreateDbContext();
        var admin = await IntegrationTestFactory.SeedUserAsync(db, "admin_start2", isAdmin: true);
        AuthAs(admin.Id, admin.Username, isAdmin: true);

        var res = await _client.PatchAsync($"/api/games/{Guid.NewGuid()}/start", null);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
