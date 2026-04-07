using System.Net;
using System.Net.Http.Json;
using FrogBets.IntegrationTests.Helpers;
using Xunit;

namespace FrogBets.IntegrationTests.Controllers;

public class UsersControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public UsersControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── GET /api/users/me ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetMe_Authenticated_Returns200WithProfile()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user   = await SeedHelper.SeedUserAsync(db, username: "meuser");
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username);

        // Act
        var res = await client.GetAsync("/api/users/me");

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<UserProfile>();
        Assert.Equal("meuser", body?.username);
        Assert.Equal(user.Id, body?.id);
    }

    [Fact]
    public async Task GetMe_Unauthenticated_Returns401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ── GET /api/users/me/balance ─────────────────────────────────────────────

    [Fact]
    public async Task GetBalance_Authenticated_Returns200WithBalance()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user   = await SeedHelper.SeedUserAsync(db, virtualBalance: 750m);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username);

        // Act
        var res = await client.GetAsync("/api/users/me/balance");

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<BalanceResponse>();
        Assert.Equal(750m, body?.virtualBalance);
    }

    [Fact]
    public async Task GetBalance_Unauthenticated_Returns401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/users/me/balance");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ── PATCH /api/users/:id/team ─────────────────────────────────────────────

    [Fact]
    public async Task MoveUserTeam_AsAdmin_Returns204()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var admin  = await SeedHelper.SeedUserAsync(db, isAdmin: true);
        var target = await SeedHelper.SeedUserAsync(db);
        var team   = await SeedHelper.SeedTeamAsync(db);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, admin.Id, admin.Username, isAdmin: true);

        // Act
        var res = await client.PatchAsJsonAsync($"/api/users/{target.Id}/team",
            new { teamId = team.Id });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task MoveUserTeam_AsNonAdminNonLeader_Returns403()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user   = await SeedHelper.SeedUserAsync(db);
        var target = await SeedHelper.SeedUserAsync(db);
        var team   = await SeedHelper.SeedTeamAsync(db);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username, isAdmin: false);

        // Act
        var res = await client.PatchAsJsonAsync($"/api/users/{target.Id}/team",
            new { teamId = team.Id });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private record UserProfile(Guid id, string username, bool isAdmin, bool isTeamLeader);
    private record BalanceResponse(decimal virtualBalance, decimal reservedBalance);
}
