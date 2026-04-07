using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FrogBets.Tests.Integration;

public class TeamsIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public TeamsIntegrationTests(IntegrationTestFactory factory)
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

    // ── GET /api/teams ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTeams_Anonymous_Returns200()
    {
        var res = await _client.GetAsync("/api/teams");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ── POST /api/teams ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTeam_AsAdmin_Returns201()
    {
        using var db = _factory.CreateDbContext();
        var admin = await IntegrationTestFactory.SeedUserAsync(db, "admin_team", isAdmin: true);
        AuthAs(admin.Id, admin.Username, isAdmin: true);

        var res = await _client.PostAsJsonAsync("/api/teams", new { name = "FrogTeam_Int" });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task CreateTeam_AsNonAdmin_Returns403()
    {
        using var db = _factory.CreateDbContext();
        var user = await IntegrationTestFactory.SeedUserAsync(db, "nonadmin_team");
        AuthAs(user.Id, user.Username);

        var res = await _client.PostAsJsonAsync("/api/teams", new { name = "SomeTeam" });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task CreateTeam_DuplicateName_Returns409()
    {
        using var db = _factory.CreateDbContext();
        var admin = await IntegrationTestFactory.SeedUserAsync(db, "admin_team2", isAdmin: true);
        AuthAs(admin.Id, admin.Username, isAdmin: true);

        await _client.PostAsJsonAsync("/api/teams", new { name = "DupTeam" });
        var res = await _client.PostAsJsonAsync("/api/teams", new { name = "DupTeam" });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ── POST /api/teams/:id/leader/:userId ────────────────────────────────────

    [Fact]
    public async Task AssignLeader_AsAdmin_Returns204()
    {
        using var db = _factory.CreateDbContext();
        var admin  = await IntegrationTestFactory.SeedUserAsync(db, "admin_leader", isAdmin: true);
        var team   = new FrogBets.Domain.Entities.CS2Team
        {
            Id        = Guid.NewGuid(),
            Name      = "LeaderTeam",
            CreatedAt = DateTime.UtcNow,
        };
        db.CS2Teams.Add(team);
        var member = await IntegrationTestFactory.SeedUserAsync(db, "leader_member");
        member.TeamId = team.Id;
        await db.SaveChangesAsync();

        AuthAs(admin.Id, admin.Username, isAdmin: true);
        var res = await _client.PostAsync($"/api/teams/{team.Id}/leader/{member.Id}", null);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    // ── GET /api/health ───────────────────────────────────────────────────────

    [Fact]
    public async Task HealthCheck_Returns200()
    {
        var res = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
