using System.Security.Claims;
using FrogBets.Domain.Entities;
using FrogBets.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly FrogBetsDbContext _db;

    public UsersController(FrogBetsDbContext db)
    {
        _db = db;
    }

    /// <summary>POST /api/users/register — creates a new user with 1000 initial balance.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var exists = await _db.Users.AnyAsync(u => u.Username == request.Username);
        if (exists)
        {
            return Conflict(new
            {
                error = new
                {
                    code = "USERNAME_TAKEN",
                    message = "Nome de usuário já está em uso."
                }
            });
        }

        var user = new User
        {
            Id             = Guid.NewGuid(),
            Username       = request.Username,
            PasswordHash   = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsAdmin        = false,
            VirtualBalance = 1000m,   // Requirement 2.2 — initial balance
            ReservedBalance = 0m,
            CreatedAt      = DateTime.UtcNow,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMe), null, new
        {
            id             = user.Id,
            username       = user.Username,
            virtualBalance = user.VirtualBalance,
        });
    }

    /// <summary>GET /api/users/me — returns the authenticated user's profile.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user is null) return NotFound();

        return Ok(new
        {
            id        = user.Id,
            username  = user.Username,
            isAdmin   = user.IsAdmin,
            createdAt = user.CreatedAt,
        });
    }

    /// <summary>GET /api/users/me/balance — returns VirtualBalance and ReservedBalance.</summary>
    [HttpGet("me/balance")]
    [Authorize]
    public async Task<IActionResult> GetBalance()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user is null) return NotFound();

        return Ok(new
        {
            virtualBalance  = user.VirtualBalance,
            reservedBalance = user.ReservedBalance,
        });
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}

public record RegisterRequest(string Username, string Password);
