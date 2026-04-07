using System.Net;
using System.Net.Http.Json;
using FrogBets.Domain.Enums;
using FrogBets.IntegrationTests.Helpers;
using Xunit;

namespace FrogBets.IntegrationTests.Controllers;

public class BetsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public BetsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── GET /api/bets ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBets_Authenticated_Returns200()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user   = await SeedHelper.SeedUserAsync(db);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username);

        // Act
        var res = await client.GetAsync("/api/bets");

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetBets_Unauthenticated_Returns401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/bets");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ── POST /api/bets ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBet_ValidRequest_Returns201()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user   = await SeedHelper.SeedUserAsync(db, virtualBalance: 500m);
        var (_, market) = await SeedHelper.SeedGameWithMarketAsync(db);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username);

        // Act
        var res = await client.PostAsJsonAsync("/api/bets", new
        {
            marketId      = market.Id,
            creatorOption = "FURIA",
            amount        = 100m,
        });

        // Assert
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task CreateBet_InsufficientBalance_Returns400WithCode()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user   = await SeedHelper.SeedUserAsync(db, virtualBalance: 10m);
        var (_, market) = await SeedHelper.SeedGameWithMarketAsync(db);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username);

        // Act
        var res = await client.PostAsJsonAsync("/api/bets", new
        {
            marketId      = market.Id,
            creatorOption = "FURIA",
            amount        = 100m,
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ErrorWrapper>();
        Assert.Equal("INSUFFICIENT_BALANCE", body?.error?.code);
    }

    [Fact]
    public async Task CreateBet_GameAlreadyStarted_Returns400WithCode()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user   = await SeedHelper.SeedUserAsync(db, virtualBalance: 500m);
        var (_, market) = await SeedHelper.SeedGameWithMarketAsync(db, GameStatus.InProgress, MarketStatus.Open);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username);

        // Act
        var res = await client.PostAsJsonAsync("/api/bets", new
        {
            marketId      = market.Id,
            creatorOption = "FURIA",
            amount        = 100m,
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ErrorWrapper>();
        Assert.Equal("GAME_ALREADY_STARTED", body?.error?.code);
    }

    // ── POST /api/bets/:id/cover ──────────────────────────────────────────────

    [Fact]
    public async Task CoverBet_ValidRequest_Returns200()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var creator = await SeedHelper.SeedUserAsync(db, virtualBalance: 500m);
        var coverer = await SeedHelper.SeedUserAsync(db, virtualBalance: 500m);
        var (_, market) = await SeedHelper.SeedGameWithMarketAsync(db);
        var bet = await SeedHelper.SeedBetAsync(db, market.Id, creator.Id);

        // Reserve creator balance manually
        creator.ReservedBalance = 100m;
        creator.VirtualBalance  = 400m;
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, coverer.Id, coverer.Username);

        // Act
        var res = await client.PostAsync($"/api/bets/{bet.Id}/cover", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task CoverBet_OwnBet_Returns400WithCode()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user   = await SeedHelper.SeedUserAsync(db, virtualBalance: 500m);
        var (_, market) = await SeedHelper.SeedGameWithMarketAsync(db);
        var bet = await SeedHelper.SeedBetAsync(db, market.Id, user.Id);

        user.ReservedBalance = 100m;
        user.VirtualBalance  = 400m;
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username);

        // Act
        var res = await client.PostAsync($"/api/bets/{bet.Id}/cover", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ErrorWrapper>();
        Assert.Equal("CANNOT_COVER_OWN_BET", body?.error?.code);
    }

    // ── DELETE /api/bets/:id ──────────────────────────────────────────────────

    [Fact]
    public async Task CancelBet_OwnPendingBet_Returns204()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user   = await SeedHelper.SeedUserAsync(db, virtualBalance: 400m);
        var (_, market) = await SeedHelper.SeedGameWithMarketAsync(db);
        var bet = await SeedHelper.SeedBetAsync(db, market.Id, user.Id);

        user.ReservedBalance = 100m;
        user.VirtualBalance  = 400m;
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username);

        // Act
        var res = await client.DeleteAsync($"/api/bets/{bet.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task CancelBet_NotOwner_Returns400WithCode()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var creator = await SeedHelper.SeedUserAsync(db, virtualBalance: 400m);
        var other   = await SeedHelper.SeedUserAsync(db, virtualBalance: 500m);
        var (_, market) = await SeedHelper.SeedGameWithMarketAsync(db);
        var bet = await SeedHelper.SeedBetAsync(db, market.Id, creator.Id);

        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, other.Id, other.Username);

        // Act
        var res = await client.DeleteAsync($"/api/bets/{bet.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ErrorWrapper>();
        Assert.Equal("NOT_BET_OWNER", body?.error?.code);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private record ErrorDetail(string code, string message);
    private record ErrorWrapper(ErrorDetail error);
}
