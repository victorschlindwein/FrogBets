using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FrogBets.Tests.Integration;

public class UsersIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public UsersIntegrationTests(IntegrationTestFactory factory)
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

    // ── GET /api/users/me ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetMe_Authenticated_ReturnsProfile()
    {
        using var db = _factory.CreateDbContext();
        var user = await IntegrationTestFactory.SeedUserAsync(db, "me_user");
        AuthAs(user.Id, user.Username);

        var res = await _client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("me_user", body);
        Assert.Contains("isAdmin", body);
        Assert.Contains("isTeamLeader", body);
    }

    [Fact]
    public async Task GetMe_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var res = await _client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ── GET /api/users/me/balance ─────────────────────────────────────────────

    [Fact]
    public async Task GetBalance_Authenticated_ReturnsBothBalances()
    {
        using var db = _factory.CreateDbContext();
        var user = await IntegrationTestFactory.SeedUserAsync(db, "balance_user", balance: 750m);
        user.ReservedBalance = 250m;
        await db.SaveChangesAsync();
        AuthAs(user.Id, user.Username);

        var res = await _client.GetAsync("/api/users/me/balance");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("virtualBalance", body);
        Assert.Contains("reservedBalance", body);
    }

    [Fact]
    public async Task GetBalance_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var res = await _client.GetAsync("/api/users/me/balance");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ── PATCH /api/users/:id/team ─────────────────────────────────────────────

    [Fact]
    public async Task MoveUserTeam_AsAdmin_Returns204()
    {
        using var db = _factory.CreateDbContext();
        var admin  = await IntegrationTestFactory.SeedUserAsync(db, "admin_move", isAdmin: true);
        var target = await IntegrationTestFactory.SeedUserAsync(db, "target_move");
        var team   = new FrogBets.Domain.Entities.CS2Team
        {
            Id        = Guid.NewGuid(),
            Name      = "MoveTeam",
            CreatedAt = DateTime.UtcNow,
        };
        db.CS2Teams.Add(team);
        await db.SaveChangesAsync();

        AuthAs(admin.Id, admin.Username, isAdmin: true);
        var res = await _client.PatchAsJsonAsync($"/api/users/{target.Id}/team",
            new { teamId = team.Id });

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task MoveUserTeam_AsNonAdmin_Returns403()
    {
        using var db = _factory.CreateDbContext();
        var user   = await IntegrationTestFactory.SeedUserAsync(db, "nonadmin_move");
        var target = await IntegrationTestFactory.SeedUserAsync(db, "target_move2");
        AuthAs(user.Id, user.Username);

        var res = await _client.PatchAsJsonAsync($"/api/users/{target.Id}/team",
            new { teamId = (Guid?)null });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
