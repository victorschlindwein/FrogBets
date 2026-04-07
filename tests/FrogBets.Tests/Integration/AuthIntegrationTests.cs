using System.Net;
using System.Net.Http.Json;
using FrogBets.Domain.Entities;
using FrogBets.Infrastructure.Data;

namespace FrogBets.Tests.Integration;

public class AuthIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public AuthIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── POST /api/auth/login ──────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        using var db = _factory.CreateDbContext();
        await IntegrationTestFactory.SeedUserAsync(db, "loginuser");

        var res = await _client.PostAsJsonAsync("/api/auth/login",
            new { username = "loginuser", password = "password123" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotEmpty(body!.Token);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        using var db = _factory.CreateDbContext();
        await IntegrationTestFactory.SeedUserAsync(db, "loginuser2");

        var res = await _client.PostAsJsonAsync("/api/auth/login",
            new { username = "loginuser2", password = "wrongpass" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUser_Returns401()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/login",
            new { username = "nobody_xyz", password = "password123" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidCredentials_SameErrorMessageForBothCases()
    {
        using var db = _factory.CreateDbContext();
        await IntegrationTestFactory.SeedUserAsync(db, "loginuser3");

        var res1 = await _client.PostAsJsonAsync("/api/auth/login",
            new { username = "nobody_xyz", password = "password123" });
        var res2 = await _client.PostAsJsonAsync("/api/auth/login",
            new { username = "loginuser3", password = "wrongpass" });

        var body1 = await res1.Content.ReadAsStringAsync();
        var body2 = await res2.Content.ReadAsStringAsync();

        // Both must return same error message (security requirement)
        Assert.Contains("Credenciais inválidas", body1);
        Assert.Contains("Credenciais inválidas", body2);
    }

    // ── POST /api/auth/register ───────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidInvite_Returns200WithToken()
    {
        using var db = _factory.CreateDbContext();
        var invite = new Invite
        {
            Id        = Guid.NewGuid(),
            Token     = "validtoken0000000000000000000001",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow,
        };
        db.Invites.Add(invite);
        await db.SaveChangesAsync();

        var res = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            inviteToken = invite.Token,
            username    = "newuser_reg",
            password    = "password123",
        });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotEmpty(body!.Token);
    }

    [Fact]
    public async Task Register_InvalidInvite_Returns400()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            inviteToken = "nonexistenttoken000000000000000",
            username    = "newuser2",
            password    = "password123",
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateUsername_Returns409()
    {
        using var db = _factory.CreateDbContext();
        await IntegrationTestFactory.SeedUserAsync(db, "existinguser");
        var invite = new Invite
        {
            Id        = Guid.NewGuid(),
            Token     = "validtoken0000000000000000000002",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow,
        };
        db.Invites.Add(invite);
        await db.SaveChangesAsync();

        var res = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            inviteToken = invite.Token,
            username    = "existinguser",
            password    = "password123",
        });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ── POST /api/auth/logout ─────────────────────────────────────────────────

    [Fact]
    public async Task Logout_WithValidToken_Returns204()
    {
        using var db = _factory.CreateDbContext();
        var user = await IntegrationTestFactory.SeedUserAsync(db, "logoutuser");
        var token = IntegrationTestFactory.GenerateToken(user.Id, user.Username);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var res = await _client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task Logout_WithoutToken_Returns401()
    {
        var res = await _client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    private record TokenResponse(string Token, DateTime ExpiresAt);
}
