using FrogBets.Api.Services;
using FrogBets.Domain.Entities;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FrogBets.Tests;

/// <summary>
/// Unit tests for AuthService — login, logout and token blocklist.
/// </summary>
public class AuthServiceTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static FrogBetsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FrogBetsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FrogBetsDbContext(options);
    }

    private static IConfiguration CreateConfig(int expirationMinutes = 60)
    {
        var dict = new Dictionary<string, string?>
        {
            ["Jwt:Key"]               = "super-secret-key-that-is-at-least-32-chars!!",
            ["Jwt:Issuer"]            = "FrogBets",
            ["Jwt:Audience"]          = "FrogBets",
            ["Jwt:ExpirationMinutes"] = expirationMinutes.ToString(),
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static AuthService CreateService(FrogBetsDbContext db, IConfiguration? config = null)
    {
        var blocklist = new TokenBlocklist();
        return new AuthService(db, config ?? CreateConfig(), blocklist);
    }

    private static async Task<User> SeedUserAsync(FrogBetsDbContext db,
        string username = "testuser", string password = "password123", bool isAdmin = false)
    {
        var user = new User
        {
            Id             = Guid.NewGuid(),
            Username       = username,
            PasswordHash   = BCrypt.Net.BCrypt.HashPassword(password),
            IsAdmin        = isAdmin,
            VirtualBalance = 1000m,
            CreatedAt      = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    // ── login — valid credentials ─────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var svc = CreateService(db);

        var result = await svc.LoginAsync("testuser", "password123");

        Assert.NotEmpty(result.Token);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_ValidCredentials_TokenExpiresAccordingToConfig()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var svc = CreateService(db, CreateConfig(expirationMinutes: 30));

        var before = DateTime.UtcNow;
        var result = await svc.LoginAsync("testuser", "password123");
        var after  = DateTime.UtcNow;

        // ExpiresAt should be ~30 minutes from now (allow 5 s tolerance)
        Assert.True(result.ExpiresAt >= before.AddMinutes(30).AddSeconds(-5));
        Assert.True(result.ExpiresAt <= after.AddMinutes(30).AddSeconds(5));
    }

    // ── login — invalid credentials ───────────────────────────────────────────

    [Fact]
    public async Task Login_WrongPassword_ThrowsUnauthorized()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var svc = CreateService(db);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.LoginAsync("testuser", "wrongpassword"));

        Assert.Equal("Credenciais inválidas", ex.Message);
    }

    [Fact]
    public async Task Login_UnknownUsername_ThrowsUnauthorized()
    {
        await using var db = CreateDb();
        var svc = CreateService(db); // no users seeded

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.LoginAsync("nobody", "password123"));

        Assert.Equal("Credenciais inválidas", ex.Message);
    }

    /// <summary>
    /// Requirement 1.3 — error message must be identical regardless of which field is wrong.
    /// </summary>
    [Fact]
    public async Task Login_InvalidCredentials_SameMessageForWrongUsernameAndWrongPassword()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db, username: "alice", password: "correct");
        var svc = CreateService(db);

        var exWrongUser = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.LoginAsync("unknown", "correct"));

        var exWrongPass = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.LoginAsync("alice", "wrong"));

        Assert.Equal(exWrongUser.Message, exWrongPass.Message);
    }

    // ── logout / blocklist ────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_ValidToken_RevokesJti()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var blocklist = new TokenBlocklist();
        var svc = new AuthService(db, CreateConfig(), blocklist);

        var result = await svc.LoginAsync("testuser", "password123");
        await svc.LogoutAsync(result.Token);

        // Extract JTI from token to verify it was blocklisted
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Token);
        Assert.True(blocklist.IsRevoked(jwt.Id));
    }

    [Fact]
    public async Task Logout_InvalidToken_DoesNotThrow()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        // Should not throw even for garbage input
        await svc.LogoutAsync("not.a.valid.token");
    }

    [Fact]
    public async Task IsTokenRevoked_UnknownJti_ReturnsFalse()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        Assert.False(svc.IsTokenRevoked(Guid.NewGuid().ToString()));
    }
}
