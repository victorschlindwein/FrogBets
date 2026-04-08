using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FrogBets.Tests.Integration;

public class PlayersIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public PlayersIntegrationTests(IntegrationTestFactory factory)
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

    private async Task<FrogBets.Domain.Entities.CS2Team> SeedTeamAsync(string name)
    {
        using var db = _factory.CreateDbContext();
        var team = new FrogBets.Domain.Entities.CS2Team
        {
            Id        = Guid.NewGuid(),
            Name      = name,
            CreatedAt = DateTime.UtcNow,
        };
        db.CS2Teams.Add(team);
        await db.SaveChangesAsync();
        return team;
    }

    // ── GET /api/players/ranking ──────────────────────────────────────────────

    [Fact]
    public async Task GetRanking_Authenticated_Returns200()
    {
        using var db = _factory.CreateDbContext();
        var user = await IntegrationTestFactory.SeedUserAsync(db, "user_ranking");
        AuthAs(user.Id, user.Username);

        var res = await _client.GetAsync("/api/players/ranking");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetRanking_Anonymous_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var res = await _client.GetAsync("/api/players/ranking");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ── GET /api/players ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetPlayers_AsAdmin_Returns200()
    {
        using var db = _factory.CreateDbContext();
        var admin = await IntegrationTestFactory.SeedUserAsync(db, "admin_player_list", isAdmin: true);
        AuthAs(admin.Id, admin.Username, isAdmin: true);

        var res = await _client.GetAsync("/api/players");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetPlayers_AsNonAdmin_Returns403()
    {
        using var db = _factory.CreateDbContext();
        var user = await IntegrationTestFactory.SeedUserAsync(db, "nonadmin_player_list");
        AuthAs(user.Id, user.Username);

        var res = await _client.GetAsync("/api/players");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
