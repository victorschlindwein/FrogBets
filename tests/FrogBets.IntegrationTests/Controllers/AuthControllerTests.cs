using System.Net;
using System.Net.Http.Json;
using FrogBets.IntegrationTests.Helpers;
using Xunit;

namespace FrogBets.IntegrationTests.Controllers;

public class AuthControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public AuthControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ── POST /api/auth/login ──────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user = await SeedHelper.SeedUserAsync(db, username: "loginuser");

        // Act
        var res = await _client.PostAsJsonAsync("/api/auth/login",
            new { username = "loginuser", password = "password123" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(body?.token);
        Assert.NotEmpty(body!.token);
    }

    [Fact]
    public async Task Login_InvalidPassword_Returns401()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        await SeedHelper.SeedUserAsync(db, username: "loginuser2");

        // Act
        var res = await _client.PostAsJsonAsync("/api/auth/login",
            new { username = "loginuser2", password = "wrongpassword" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUser_Returns401()
    {
        // Act
        var res = await _client.PostAsJsonAsync("/api/auth/login",
            new { username = "nobody_xyz", password = "password123" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ErrorCodeIsInvalidCredentials()
    {
        // Act
        var res = await _client.PostAsJsonAsync("/api/auth/login",
            new { username = "nobody_xyz2", password = "wrong" });

        // Assert
        var body = await res.Content.ReadFromJsonAsync<ErrorWrapper>();
        Assert.Equal("INVALID_CREDENTIALS", body?.error?.code);
    }

    // ── POST /api/auth/logout ─────────────────────────────────────────────────

    [Fact]
    public async Task Logout_AuthenticatedUser_Returns204()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user = await SeedHelper.SeedUserAsync(db, username: "logoutuser");
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username);

        // Act
        var res = await client.PostAsync("/api/auth/logout", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task Logout_Unauthenticated_Returns401()
    {
        var res = await _client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ── POST /api/auth/register ───────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidInvite_Returns200WithToken()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var invite = await SeedHelper.SeedInviteAsync(db);

        // Act
        var res = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            inviteToken = invite.Token,
            username    = "newuser_" + Guid.NewGuid().ToString("N")[..6],
            password    = "password123",
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(body?.token);
    }

    [Fact]
    public async Task Register_InvalidInviteToken_Returns400WithCode()
    {
        // Act
        var res = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            inviteToken = "totally-invalid-token",
            username    = "someuser",
            password    = "password123",
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ErrorWrapper>();
        Assert.Equal("INVALID_INVITE", body?.error?.code);
    }

    [Fact]
    public async Task Register_UsedInvite_Returns400WithCode()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var invite = await SeedHelper.SeedInviteAsync(db, used: true);

        // Act
        var res = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            inviteToken = invite.Token,
            username    = "someuser2",
            password    = "password123",
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ErrorWrapper>();
        Assert.Equal("INVITE_ALREADY_USED", body?.error?.code);
    }

    [Fact]
    public async Task Register_DuplicateUsername_Returns409WithCode()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var invite1 = await SeedHelper.SeedInviteAsync(db);
        var invite2 = await SeedHelper.SeedInviteAsync(db);
        var username = "dupuser_" + Guid.NewGuid().ToString("N")[..6];

        // First registration
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            inviteToken = invite1.Token,
            username,
            password = "password123",
        });

        // Act — second registration with same username
        var res = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            inviteToken = invite2.Token,
            username,
            password = "password123",
        });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ErrorWrapper>();
        Assert.Equal("USERNAME_TAKEN", body?.error?.code);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private record TokenResponse(string token, DateTime expiresAt);
    private record ErrorDetail(string code, string message);
    private record ErrorWrapper(ErrorDetail error);
}
