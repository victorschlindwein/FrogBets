using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FrogBets.Domain.Entities;

namespace FrogBets.Tests.Integration;

public class InvitesIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public InvitesIntegrationTests(IntegrationTestFactory factory)
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

    // ── POST /api/invites ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateInvite_AsAdmin_Returns201WithToken()
    {
        using var db = _factory.CreateDbContext();
        var admin = await IntegrationTestFactory.SeedUserAsync(db, "admin_invite", isAdmin: true);
        AuthAs(admin.Id, admin.Username, isAdmin: true);

        var res = await _client.PostAsJsonAsync("/api/invites", new
        {
            expiresAt   = DateTime.UtcNow.AddDays(7).ToString("o"),
            description = "Test invite",
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("token", body);
    }

    [Fact]
    public async Task CreateInvite_AsNonAdmin_Returns403()
    {
        using var db = _factory.CreateDbContext();
        var user = await IntegrationTestFactory.SeedUserAsync(db, "nonadmin_invite");
        AuthAs(user.Id, user.Username);

        var res = await _client.PostAsJsonAsync("/api/invites", new
        {
            expiresAt = DateTime.UtcNow.AddDays(7).ToString("o"),
        });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ── GET /api/invites ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetInvites_AsAdmin_Returns200()
    {
        using var db = _factory.CreateDbContext();
        var admin = await IntegrationTestFactory.SeedUserAsync(db, "admin_invite_list", isAdmin: true);
        AuthAs(admin.Id, admin.Username, isAdmin: true);

        var res = await _client.GetAsync("/api/invites");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetInvites_AsNonAdmin_Returns403()
    {
        using var db = _factory.CreateDbContext();
        var user = await IntegrationTestFactory.SeedUserAsync(db, "nonadmin_invite_list");
        AuthAs(user.Id, user.Username);

        var res = await _client.GetAsync("/api/invites");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ── DELETE /api/invites/:id ───────────────────────────────────────────────

    [Fact]
    public async Task RevokeInvite_PendingInvite_Returns204()
    {
        using var db = _factory.CreateDbContext();
        var admin = await IntegrationTestFactory.SeedUserAsync(db, "admin_revoke", isAdmin: true);
        var invite = new Invite
        {
            Id        = Guid.NewGuid(),
            Token     = "revoketoken000000000000000000001",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow,
        };
        db.Invites.Add(invite);
        await db.SaveChangesAsync();

        AuthAs(admin.Id, admin.Username, isAdmin: true);
        var res = await _client.DeleteAsync($"/api/invites/{invite.Id}");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task RevokeInvite_NonExistent_Returns404()
    {
        using var db = _factory.CreateDbContext();
        var admin = await IntegrationTestFactory.SeedUserAsync(db, "admin_revoke2", isAdmin: true);
        AuthAs(admin.Id, admin.Username, isAdmin: true);

        var res = await _client.DeleteAsync($"/api/invites/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
