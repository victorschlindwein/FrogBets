using System.Net;
using System.Net.Http.Json;
using FrogBets.Domain.Enums;
using FrogBets.IntegrationTests.Helpers;
using Xunit;

namespace FrogBets.IntegrationTests.Controllers;

public class GamesControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public GamesControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ── GET /api/games ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetGames_ReturnsOkWithList()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        await SeedHelper.SeedGameWithMarketAsync(db);

        // Act
        var res = await _client.GetAsync("/api/games");

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<List<object>>();
        Assert.NotNull(body);
    }

    [Fact]
    public async Task GetGames_IsPublic_NoAuthRequired()
    {
        var res = await _client.GetAsync("/api/games");
        Assert.NotEqual(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ── GET /api/games/:id ────────────────────────────────────────────────────

    [Fact]
    public async Task GetGame_ExistingId_Returns200()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var (game, _) = await SeedHelper.SeedGameWithMarketAsync(db);

        // Act
        var res = await _client.GetAsync($"/api/games/{game.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetGame_UnknownId_Returns404()
    {
        var res = await _client.GetAsync($"/api/games/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ── POST /api/games ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateGame_AsAdmin_Returns201()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var admin  = await SeedHelper.SeedUserAsync(db, isAdmin: true);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, admin.Id, admin.Username, isAdmin: true);

        // Act
        var res = await client.PostAsJsonAsync("/api/games", new
        {
            teamA        = "FURIA",
            teamB        = "NAVI",
            scheduledAt  = DateTime.UtcNow.AddDays(1),
            numberOfMaps = 1,
        });

        // Assert
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task CreateGame_AsNonAdmin_Returns403()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user   = await SeedHelper.SeedUserAsync(db);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username, isAdmin: false);

        // Act
        var res = await client.PostAsJsonAsync("/api/games", new
        {
            teamA        = "FURIA",
            teamB        = "NAVI",
            scheduledAt  = DateTime.UtcNow.AddDays(1),
            numberOfMaps = 1,
        });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ── PATCH /api/games/:id/start ────────────────────────────────────────────

    [Fact]
    public async Task StartGame_AsAdmin_Returns204()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var (game, _) = await SeedHelper.SeedGameWithMarketAsync(db);
        var admin  = await SeedHelper.SeedUserAsync(db, isAdmin: true);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, admin.Id, admin.Username, isAdmin: true);

        // Act
        var res = await client.PatchAsync($"/api/games/{game.Id}/start", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task StartGame_UnknownId_Returns404()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var admin  = await SeedHelper.SeedUserAsync(db, isAdmin: true);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, admin.Id, admin.Username, isAdmin: true);

        // Act
        var res = await client.PatchAsync($"/api/games/{Guid.NewGuid()}/start", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ── POST /api/games/:id/results ───────────────────────────────────────────

    [Fact]
    public async Task RegisterResult_AsAdmin_Returns204()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var (game, market) = await SeedHelper.SeedGameWithMarketAsync(db, GameStatus.InProgress, MarketStatus.Closed);
        var admin  = await SeedHelper.SeedUserAsync(db, isAdmin: true);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, admin.Id, admin.Username, isAdmin: true);

        // Act
        var res = await client.PostAsJsonAsync($"/api/games/{game.Id}/results", new
        {
            marketId      = market.Id,
            winningOption = "FURIA",
            mapNumber     = 1,
        });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }
}
