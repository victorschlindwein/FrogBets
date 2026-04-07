using System.Security.Claims;
using FrogBets.Api.Services;
using FrogBets.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FrogBets.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly FrogBetsDbContext _db;
    private readonly ITeamMembershipService _teamMembershipService;
    private readonly string _masterAdminUsername;

    public UsersController(FrogBetsDbContext db, ITeamMembershipService teamMembershipService, IConfiguration config)
    {
        _db = db;
        _teamMembershipService = teamMembershipService;
        _masterAdminUsername = config["MasterAdminUsername"] ?? string.Empty;
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
            id           = user.Id,
            username     = user.Username,
            isAdmin      = user.IsAdmin,
            isTeamLeader = user.IsTeamLeader,
            teamId       = user.TeamId,
            createdAt    = user.CreatedAt,
            isMasterAdmin = !string.IsNullOrEmpty(_masterAdminUsername) &&
                            user.Username.Equals(_masterAdminUsername, StringComparison.OrdinalIgnoreCase),
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

    /// <summary>PATCH /api/users/{id}/team — admin or team leader: move user to a team (or remove).</summary>
    [HttpPatch("{id:guid}/team")]
    [Authorize]
    public async Task<IActionResult> MoveUserTeam(Guid id, [FromBody] MoveUserTeamBody body)
    {
        var requesterId = GetCurrentUserId();
        if (requesterId is null) return Unauthorized();

        var requesterIsAdmin = await IsAdminFromDb();

        if (!requesterIsAdmin)
        {
            var requester = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == requesterId.Value);
            if (requester is null || !requester.IsTeamLeader)
                return StatusCode(403, new { error = new { code = "FORBIDDEN", message = "Acesso negado." } });
        }

        try
        {
            await _teamMembershipService.MoveUserAsync(requesterId.Value, requesterIsAdmin, id, body.TeamId);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "USER_NOT_FOUND")
        {
            return NotFound(new { error = new { code = ex.Message, message = "Usuário não encontrado." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "TEAM_NOT_FOUND")
        {
            return NotFound(new { error = new { code = ex.Message, message = "Time não encontrado." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "FORBIDDEN")
        {
            return StatusCode(403, new { error = new { code = ex.Message, message = "Acesso negado." } });
        }
    }

    /// <summary>GET /api/users — admin: list all users with id, username, isAdmin, teamId.</summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ListUsers()
    {
        if (!await IsAdminFromDb())
            return StatusCode(403, new { error = new { code = "FORBIDDEN", message = "Acesso negado." } });

        var users = await _db.Users.AsNoTracking()
            .OrderBy(u => u.Username)
            .Select(u => new
            {
                id           = u.Id,
                username     = u.Username,
                isAdmin      = u.IsAdmin,
                isTeamLeader = u.IsTeamLeader,
                teamId       = u.TeamId,
                createdAt    = u.CreatedAt,
            })
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>POST /api/users/{id}/promote — admin: grant admin role to a user.</summary>
    [HttpPost("{id:guid}/promote")]
    [Authorize]
    public async Task<IActionResult> PromoteToAdmin(Guid id)
    {
        if (!await IsAdminFromDb())
            return StatusCode(403, new { error = new { code = "FORBIDDEN", message = "Acesso negado." } });

        var target = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (target is null)
            return NotFound(new { error = new { code = "USER_NOT_FOUND", message = "Usuário não encontrado." } });

        target.IsAdmin = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>POST /api/users/{id}/demote — admin: revoke admin role from a user. Cannot demote master admin.</summary>
    [HttpPost("{id:guid}/demote")]
    [Authorize]
    public async Task<IActionResult> DemoteFromAdmin(Guid id)
    {
        if (!await IsAdminFromDb())
            return StatusCode(403, new { error = new { code = "FORBIDDEN", message = "Acesso negado." } });

        var target = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (target is null)
            return NotFound(new { error = new { code = "USER_NOT_FOUND", message = "Usuário não encontrado." } });

        // Protege o usuário master de ter seus direitos revogados
        if (!string.IsNullOrEmpty(_masterAdminUsername) &&
            target.Username.Equals(_masterAdminUsername, StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, new { error = new { code = "FORBIDDEN", message = "Não é possível revogar os direitos do administrador master." } });

        target.IsAdmin = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private async Task<bool> IsAdminFromDb()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return false;
        return await _db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId.Value && u.IsAdmin);
    }
}

public record MoveUserTeamBody(Guid? TeamId);
