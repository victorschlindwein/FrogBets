using System.Security.Claims;
using FrogBets.Api.Services;
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
    private readonly ITeamMembershipService _teamMembershipService;

    public UsersController(FrogBetsDbContext db, ITeamMembershipService teamMembershipService)
    {
        _db = db;
        _teamMembershipService = teamMembershipService;
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

    /// <summary>PATCH /api/users/{id}/team — admin or team leader: move user to a team (or remove).</summary>
    [HttpPatch("{id:guid}/team")]
    [Authorize]
    public async Task<IActionResult> MoveUserTeam(Guid id, [FromBody] MoveUserTeamBody body)
    {
        var requesterId = GetCurrentUserId();
        if (requesterId is null) return Unauthorized();

        var requesterIsAdmin = User.FindFirstValue("isAdmin") == "true";

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

    // ── helpers ───────────────────────────────────────────────────────────────

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}

public record MoveUserTeamBody(Guid? TeamId);
