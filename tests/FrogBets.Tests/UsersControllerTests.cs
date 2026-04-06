using System.Security.Claims;
using FrogBets.Api.Controllers;
using FrogBets.Domain.Entities;
using FrogBets.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Tests;

/// <summary>
/// Unit tests for UsersController — register, profile and balance endpoints.
/// Requirements: 1.2, 2.1, 2.2, 2.9
/// </summary>
public class UsersControllerTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static FrogBetsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FrogBetsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FrogBetsDbContext(options);
    }

    private static UsersController CreateController(FrogBetsDbContext db, Guid? authenticatedUserId = null)
    {
        var controller = new UsersController(db);

        if (authenticatedUserId.HasValue)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, authenticatedUserId.Value.ToString()),
                new Claim(ClaimTypes.Name, "testuser"),
            };
            var identity  = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        return controller;
    }

    private static async Task<User> SeedUserAsync(FrogBetsDbContext db,
        string username = "existing", string password = "pass123")
    {
        var user = new User
        {
            Id              = Guid.NewGuid(),
            Username        = username,
            PasswordHash    = BCrypt.Net.BCrypt.HashPassword(password),
            IsAdmin         = false,
            VirtualBalance  = 1000m,
            ReservedBalance = 0m,
            CreatedAt       = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    // ── POST /api/users/register ──────────────────────────────────────────────

    [Fact]
    public async Task Register_NewUser_Returns201WithIdUsernameAndBalance()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        var result = await controller.Register(new RegisterRequest("alice", "secret123"));

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, created.StatusCode);

        var json = System.Text.Json.JsonSerializer.Serialize(created.Value);
        Assert.Contains("alice", json);
        Assert.Contains("1000", json);
        // id must be a non-empty GUID
        Assert.Matches(@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", json);
    }

    /// <summary>Requirement 2.2 — new user gets 1000 initial virtual balance.</summary>
    [Fact]
    public async Task Register_NewUser_AssignsInitialBalanceOf1000()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        await controller.Register(new RegisterRequest("bob", "pass"));

        var user = await db.Users.SingleAsync(u => u.Username == "bob");
        Assert.Equal(1000m, user.VirtualBalance);
        Assert.Equal(0m, user.ReservedBalance);
    }

    [Fact]
    public async Task Register_NewUser_PasswordIsHashed()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        await controller.Register(new RegisterRequest("carol", "mypassword"));

        var user = await db.Users.SingleAsync(u => u.Username == "carol");
        Assert.NotEqual("mypassword", user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("mypassword", user.PasswordHash));
    }

    [Fact]
    public async Task Register_DuplicateUsername_Returns409WithUsernameTakenCode()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db, username: "dave");
        var controller = CreateController(db);

        var result = await controller.Register(new RegisterRequest("dave", "newpass"));

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);

        // Verify error structure
        var json = System.Text.Json.JsonSerializer.Serialize(conflict.Value);
        Assert.Contains("USERNAME_TAKEN", json);
    }

    // ── GET /api/users/me ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetMe_AuthenticatedUser_ReturnsProfileFields()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, username: "eve");
        var controller = CreateController(db, authenticatedUserId: user.Id);

        var result = await controller.GetMe();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("eve", json);
        Assert.Contains(user.Id.ToString(), json);
    }

    [Fact]
    public async Task GetMe_UnknownUserId_Returns404()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, authenticatedUserId: Guid.NewGuid());

        var result = await controller.GetMe();

        Assert.IsType<NotFoundResult>(result);
    }

    // ── GET /api/users/me/balance ─────────────────────────────────────────────

    /// <summary>Requirement 2.9 — user can see available and reserved balance.</summary>
    [Fact]
    public async Task GetBalance_AuthenticatedUser_ReturnsVirtualAndReservedBalance()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, username: "frank");
        // Manually set a reserved balance to verify both fields
        user.VirtualBalance  = 750m;
        user.ReservedBalance = 250m;
        await db.SaveChangesAsync();

        var controller = CreateController(db, authenticatedUserId: user.Id);

        var result = await controller.GetBalance();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("750", json);
        Assert.Contains("250", json);
    }

    [Fact]
    public async Task GetBalance_UnknownUserId_Returns404()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, authenticatedUserId: Guid.NewGuid());

        var result = await controller.GetBalance();

        Assert.IsType<NotFoundResult>(result);
    }
}
