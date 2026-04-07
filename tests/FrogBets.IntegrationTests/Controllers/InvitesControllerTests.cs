using System.Net;
using System.Net.Http.Json;
using FrogBets.IntegrationTests.Helpers;
using Xunit;

namespace FrogBets.IntegrationTests.Controllers;

public class InvitesControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public InvitesControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── POST /api/invites ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateInvite_AsAdmin_Returns201()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var admin  = await SeedHelper.SeedUserAsync(db, isAdmin: true);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, admin.Id, admin.Username, isAdmin: true);

        // Act
        var res = await client.PostAsJsonAsync("/api/invites", new
        {
            expiresAt   = DateTime.UtcNow.AddDays(7),
            description = "Test invite",
        });

        // Assert
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<InviteResponse>();
        Assert.NotNull(body?.token);
        Assert.Equal(32, body!.token.Length);
    }

    [Fact]
    public async Task CreateInvite_AsNonAdmin_Returns403()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user   = await SeedHelper.SeedUserAsync(db);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username, isAdmin: false);

        // Act
        var res = await client.PostAsJsonAsync("/api/invites", new
        {
            expiresAt   = DateTime.UtcNow.AddDays(7),
            description = "Test",
        });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ── GET /api/invites ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetInvites_AsAdmin_Returns200WithList()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var admin  = await SeedHelper.SeedUserAsync(db, isAdmin: true);
        await SeedHelper.SeedInviteAsync(db);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, admin.Id, admin.Username, isAdmin: true);

        // Act
        var res = await client.GetAsync("/api/invites");

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<List<InviteResponse>>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!);
    }

    [Fact]
    public async Task GetInvites_AsNonAdmin_Returns403()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user   = await SeedHelper.SeedUserAsync(db);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username, isAdmin: false);

        // Act
        var res = await client.GetAsync("/api/invites");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ── DELETE /api/invites/:id ───────────────────────────────────────────────

    [Fact]
    public async Task RevokeInvite_PendingInvite_Returns204()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var admin  = await SeedHelper.SeedUserAsync(db, isAdmin: true);
        var invite = await SeedHelper.SeedInviteAsync(db);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, admin.Id, admin.Username, isAdmin: true);

        // Act
        var res = await client.DeleteAsync($"/api/invites/{invite.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task RevokeInvite_UsedInvite_Returns400WithCode()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var admin  = await SeedHelper.SeedUserAsync(db, isAdmin: true);
        var invite = await SeedHelper.SeedInviteAsync(db, used: true);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, admin.Id, admin.Username, isAdmin: true);

        // Act
        var res = await client.DeleteAsync($"/api/invites/{invite.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ErrorWrapper>();
        Assert.Equal("INVITE_ALREADY_USED", body?.error?.code);
    }

    [Fact]
    public async Task RevokeInvite_UnknownId_Returns404()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var admin  = await SeedHelper.SeedUserAsync(db, isAdmin: true);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, admin.Id, admin.Username, isAdmin: true);

        // Act
        var res = await client.DeleteAsync($"/api/invites/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private record InviteResponse(Guid id, string token, string? description, DateTime expiresAt, DateTime createdAt, string status);
    private record ErrorDetail(string code, string message);
    private record ErrorWrapper(ErrorDetail error);
}
