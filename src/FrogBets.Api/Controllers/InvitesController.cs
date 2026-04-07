using System.Security.Claims;
using FrogBets.Api.Services;
using FrogBets.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Controllers;

[ApiController]
[Route("api/invites")]
[Authorize]
public class InvitesController : ControllerBase
{
    private readonly IInviteService _inviteService;
    private readonly FrogBetsDbContext _db;

    public InvitesController(IInviteService inviteService, FrogBetsDbContext db)
    {
        _inviteService = inviteService;
        _db = db;
    }

    /// <summary>POST /api/invites — admin: generate a new invite token.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateInvite([FromBody] CreateInviteRequest request)
    {
        if (!await IsAdminFromDb()) return Forbid();

        var result = await _inviteService.GenerateAsync(request.ExpiresAt, request.Description);
        return StatusCode(201, ToResponse(result));
    }

    /// <summary>GET /api/invites — admin: list all invites.</summary>
    [HttpGet]
    public async Task<IActionResult> GetInvites()
    {
        if (!await IsAdminFromDb()) return Forbid();

        var invites = await _inviteService.GetAllAsync();
        return Ok(invites.Select(ToResponse));
    }

    /// <summary>DELETE /api/invites/{id} — admin: revoke a pending invite.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RevokeInvite(Guid id)
    {
        if (!await IsAdminFromDb()) return Forbid();

        try
        {
            await _inviteService.RevokeAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVITE_ALREADY_USED")
        {
            return BadRequest(new { error = new { code = "INVITE_ALREADY_USED", message = "O convite já foi utilizado." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVITE_ALREADY_EXPIRED")
        {
            return BadRequest(new { error = new { code = "INVITE_ALREADY_EXPIRED", message = "O convite já está expirado." } });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = new { code = "INVITE_NOT_FOUND", message = "Convite não encontrado." } });
        }
    }

    private bool IsAdmin() =>
        User.FindFirstValue("isAdmin") == "true";

    private async Task<bool> IsAdminFromDb()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId)) return false;
        return await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsAdmin);
    }

    private static InviteResponse ToResponse(InviteResult r) =>
        new(r.Id, r.Token, r.Description, r.ExpiresAt, r.CreatedAt, r.Status.ToString());
}

public record CreateInviteRequest(DateTime ExpiresAt, string? Description);
public record InviteResponse(Guid Id, string Token, string? Description, DateTime ExpiresAt, DateTime CreatedAt, string Status);
