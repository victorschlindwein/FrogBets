using System.Security.Claims;
using FrogBets.Api.Controllers;
using FrogBets.Api.Services;
using FrogBets.Domain.Entities;
using FrogBets.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Tests;

/// <summary>
/// Unit tests for UsersController — profile and balance endpoints.
/// Requirements: 1.2, 2.9
/// Note: Registration is handled by AuthController (invite-based). See AuthServiceTests.
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
        var teamMembershipService = new StubTeamMembershipService();
        var controller = new UsersController(db, teamMembershipService);

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

// Stub for ITeamMembershipService — not used in these tests
file class StubTeamMembershipService : ITeamMembershipService
{
    public Task AssignLeaderAsync(Guid teamId, Guid userId) => Task.CompletedTask;
    public Task RemoveLeaderAsync(Guid teamId) => Task.CompletedTask;
    public Task MoveUserAsync(Guid requesterId, bool requesterIsAdmin, Guid targetUserId, Guid? destinationTeamId) => Task.CompletedTask;
}
