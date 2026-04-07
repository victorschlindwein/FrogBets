using System.Net;
using System.Net.Http.Json;
using FrogBets.Domain.Entities;
using FrogBets.IntegrationTests.Helpers;
using Xunit;

namespace FrogBets.IntegrationTests.Controllers;

public class TradesControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public TradesControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── GET /api/trades/listings ──────────────────────────────────────────────

    [Fact]
    public async Task GetListings_Authenticated_Returns200()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user   = await SeedHelper.SeedUserAsync(db);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username);

        // Act
        var res = await client.GetAsync("/api/trades/listings");

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetListings_Unauthenticated_Returns401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/trades/listings");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ── POST /api/trades/listings ─────────────────────────────────────────────

    [Fact]
    public async Task AddListing_AsTeamLeader_Returns204()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var team   = await SeedHelper.SeedTeamAsync(db);
        var leader = await SeedHelper.SeedUserAsync(db, isTeamLeader: true, teamId: team.Id);
        var member = await SeedHelper.SeedUserAsync(db, teamId: team.Id);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, leader.Id, leader.Username);

        // Act
        var res = await client.PostAsJsonAsync("/api/trades/listings", new { userId = member.Id });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task AddListing_AsNonLeader_Returns403()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var team   = await SeedHelper.SeedTeamAsync(db);
        var user   = await SeedHelper.SeedUserAsync(db, teamId: team.Id);
        var member = await SeedHelper.SeedUserAsync(db, teamId: team.Id);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username);

        // Act
        var res = await client.PostAsJsonAsync("/api/trades/listings", new { userId = member.Id });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task AddListing_AlreadyListed_Returns409()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var team   = await SeedHelper.SeedTeamAsync(db);
        var leader = await SeedHelper.SeedUserAsync(db, isTeamLeader: true, teamId: team.Id);
        var member = await SeedHelper.SeedUserAsync(db, teamId: team.Id);

        // Seed existing listing
        db.TradeListings.Add(new TradeListing
        {
            Id        = Guid.NewGuid(),
            UserId    = member.Id,
            TeamId    = team.Id,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, leader.Id, leader.Username);

        // Act
        var res = await client.PostAsJsonAsync("/api/trades/listings", new { userId = member.Id });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ErrorWrapper>();
        Assert.Equal("ALREADY_LISTED", body?.error?.code);
    }

    // ── DELETE /api/trades/listings/:userId ───────────────────────────────────

    [Fact]
    public async Task RemoveListing_AsTeamLeader_Returns204()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var team   = await SeedHelper.SeedTeamAsync(db);
        var leader = await SeedHelper.SeedUserAsync(db, isTeamLeader: true, teamId: team.Id);
        var member = await SeedHelper.SeedUserAsync(db, teamId: team.Id);

        db.TradeListings.Add(new TradeListing
        {
            Id        = Guid.NewGuid(),
            UserId    = member.Id,
            TeamId    = team.Id,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, leader.Id, leader.Username);

        // Act
        var res = await client.DeleteAsync($"/api/trades/listings/{member.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    // ── POST /api/trades/offers ───────────────────────────────────────────────

    [Fact]
    public async Task CreateOffer_AsTeamLeader_Returns201()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var teamA  = await SeedHelper.SeedTeamAsync(db);
        var teamB  = await SeedHelper.SeedTeamAsync(db);
        var leader = await SeedHelper.SeedUserAsync(db, isTeamLeader: true, teamId: teamA.Id);
        var offered = await SeedHelper.SeedUserAsync(db, teamId: teamA.Id);
        var target  = await SeedHelper.SeedUserAsync(db, teamId: teamB.Id);

        db.TradeListings.Add(new TradeListing
        {
            Id        = Guid.NewGuid(),
            UserId    = target.Id,
            TeamId    = teamB.Id,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, leader.Id, leader.Username);

        // Act
        var res = await client.PostAsJsonAsync("/api/trades/offers", new
        {
            offeredUserId = offered.Id,
            targetUserId  = target.Id,
        });

        // Assert
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task CreateOffer_TargetNotListed_Returns400WithCode()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var teamA  = await SeedHelper.SeedTeamAsync(db);
        var teamB  = await SeedHelper.SeedTeamAsync(db);
        var leader = await SeedHelper.SeedUserAsync(db, isTeamLeader: true, teamId: teamA.Id);
        var offered = await SeedHelper.SeedUserAsync(db, teamId: teamA.Id);
        var target  = await SeedHelper.SeedUserAsync(db, teamId: teamB.Id);

        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, leader.Id, leader.Username);

        // Act
        var res = await client.PostAsJsonAsync("/api/trades/offers", new
        {
            offeredUserId = offered.Id,
            targetUserId  = target.Id,
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ErrorWrapper>();
        Assert.Equal("TARGET_NOT_AVAILABLE", body?.error?.code);
    }

    // ── PATCH /api/trades/offers/:id/accept ──────────────────────────────────

    [Fact]
    public async Task AcceptOffer_AsReceiverLeader_Returns204()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var teamA   = await SeedHelper.SeedTeamAsync(db);
        var teamB   = await SeedHelper.SeedTeamAsync(db);
        var leaderA = await SeedHelper.SeedUserAsync(db, isTeamLeader: true, teamId: teamA.Id);
        var leaderB = await SeedHelper.SeedUserAsync(db, isTeamLeader: true, teamId: teamB.Id);
        var offered = await SeedHelper.SeedUserAsync(db, teamId: teamA.Id);
        var target  = await SeedHelper.SeedUserAsync(db, teamId: teamB.Id);

        var offer = new TradeOffer
        {
            Id             = Guid.NewGuid(),
            OfferedUserId  = offered.Id,
            TargetUserId   = target.Id,
            ProposerTeamId = teamA.Id,
            ReceiverTeamId = teamB.Id,
            Status         = TradeOfferStatus.Pending,
            CreatedAt      = DateTime.UtcNow,
            UpdatedAt      = DateTime.UtcNow,
        };
        db.TradeOffers.Add(offer);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, leaderB.Id, leaderB.Username);

        // Act
        var res = await client.PatchAsync($"/api/trades/offers/{offer.Id}/accept", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    // ── PATCH /api/trades/offers/:id/reject ──────────────────────────────────

    [Fact]
    public async Task RejectOffer_AsReceiverLeader_Returns204()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var teamA   = await SeedHelper.SeedTeamAsync(db);
        var teamB   = await SeedHelper.SeedTeamAsync(db);
        var leaderB = await SeedHelper.SeedUserAsync(db, isTeamLeader: true, teamId: teamB.Id);
        var offered = await SeedHelper.SeedUserAsync(db, teamId: teamA.Id);
        var target  = await SeedHelper.SeedUserAsync(db, teamId: teamB.Id);

        var offer = new TradeOffer
        {
            Id             = Guid.NewGuid(),
            OfferedUserId  = offered.Id,
            TargetUserId   = target.Id,
            ProposerTeamId = teamA.Id,
            ReceiverTeamId = teamB.Id,
            Status         = TradeOfferStatus.Pending,
            CreatedAt      = DateTime.UtcNow,
            UpdatedAt      = DateTime.UtcNow,
        };
        db.TradeOffers.Add(offer);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, leaderB.Id, leaderB.Username);

        // Act
        var res = await client.PatchAsync($"/api/trades/offers/{offer.Id}/reject", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    // ── POST /api/trades/direct ───────────────────────────────────────────────

    [Fact]
    public async Task DirectSwap_AsAdmin_Returns204()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var teamA  = await SeedHelper.SeedTeamAsync(db);
        var teamB  = await SeedHelper.SeedTeamAsync(db);
        var admin  = await SeedHelper.SeedUserAsync(db, isAdmin: true);
        var userA  = await SeedHelper.SeedUserAsync(db, teamId: teamA.Id);
        var userB  = await SeedHelper.SeedUserAsync(db, teamId: teamB.Id);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, admin.Id, admin.Username, isAdmin: true);

        // Act
        var res = await client.PostAsJsonAsync("/api/trades/direct", new
        {
            userAId = userA.Id,
            userBId = userB.Id,
        });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task DirectSwap_SameTeam_Returns400WithCode()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var team   = await SeedHelper.SeedTeamAsync(db);
        var admin  = await SeedHelper.SeedUserAsync(db, isAdmin: true);
        var userA  = await SeedHelper.SeedUserAsync(db, teamId: team.Id);
        var userB  = await SeedHelper.SeedUserAsync(db, teamId: team.Id);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, admin.Id, admin.Username, isAdmin: true);

        // Act
        var res = await client.PostAsJsonAsync("/api/trades/direct", new
        {
            userAId = userA.Id,
            userBId = userB.Id,
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ErrorWrapper>();
        Assert.Equal("SAME_TEAM_TRADE", body?.error?.code);
    }

    [Fact]
    public async Task DirectSwap_AsNonAdmin_Returns403()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user   = await SeedHelper.SeedUserAsync(db);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username, isAdmin: false);

        // Act
        var res = await client.PostAsJsonAsync("/api/trades/direct", new
        {
            userAId = Guid.NewGuid(),
            userBId = Guid.NewGuid(),
        });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private record ErrorDetail(string code, string message);
    private record ErrorWrapper(ErrorDetail error);
}
