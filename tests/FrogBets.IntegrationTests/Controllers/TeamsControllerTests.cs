using System.Net;
using System.Net.Http.Json;
using FrogBets.IntegrationTests.Helpers;
using Xunit;

namespace FrogBets.IntegrationTests.Controllers;

public class TeamsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public TeamsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── POST /api/teams ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTeam_AsAdmin_Returns201()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var admin  = await SeedHelper.SeedUserAsync(db, isAdmin: true);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, admin.Id, admin.Username, isAdmin: true);

        // Act
        var res = await client.PostAsJsonAsync("/api/teams", new
        {
            name    = "Team_" + Guid.NewGuid().ToString("N")[..6],
            logoUrl = (string?)null,
        });

        // Assert
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task CreateTeam_AsNonAdmin_Returns403()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user   = await SeedHelper.SeedUserAsync(db);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username, isAdmin: false);

        // Act
        var res = await client.PostAsJsonAsync("/api/teams", new
        {
            name    = "SomeTeam",
            logoUrl = (string?)null,
        });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task CreateTeam_DuplicateName_Returns409()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var admin  = await SeedHelper.SeedUserAsync(db, isAdmin: true);
        var team   = await SeedHelper.SeedTeamAsync(db, name: "UniqueTeamName");
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, admin.Id, admin.Username, isAdmin: true);

        // Act
        var res = await client.PostAsJsonAsync("/api/teams", new
        {
            name    = "UniqueTeamName",
            logoUrl = (string?)null,
        });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ── GET /api/teams ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTeams_IsPublic_Returns200()
    {
        var res = await _factory.CreateClient().GetAsync("/api/teams");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetTeams_ReturnsSeededTeams()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        await SeedHelper.SeedTeamAsync(db, name: "TeamForListing_" + Guid.NewGuid().ToString("N")[..4]);

        // Act
        var res  = await _factory.CreateClient().GetAsync("/api/teams");
        var body = await res.Content.ReadFromJsonAsync<List<TeamResponse>>();

        // Assert
        Assert.NotNull(body);
        Assert.NotEmpty(body!);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private record TeamResponse(Guid id, string name, string? logoUrl, DateTime createdAt);
}
