using System.ComponentModel.DataAnnotations;
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

    /// <summary>POST /api/invites — admin: generate one or more invite tokens.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateInvites([FromBody] CreateInvitesRequest request)
    {
        if (!await IsAdminFromDb()) return Forbid();

        if (request.Quantity < 1)
            return BadRequest(new { error = new { code = "INVALID_QUANTITY", message = "A quantidade deve ser um número inteiro maior que 0." } });

        if (request.Quantity > 50)
            return BadRequest(new { error = new { code = "QUANTITY_LIMIT_EXCEEDED", message = "A quantidade máxima por requisição é 50." } });

        var description = request.Quantity == 1 ? request.Description : null;
        var tokens = new List<string>(request.Quantity);

        for (int i = 0; i < request.Quantity; i++)
        {
            var result = await _inviteService.GenerateAsync(description);
            tokens.Add(result.Token);
        }

        return StatusCode(201, new CreateInvitesResponse(tokens));
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

public record CreateInvitesRequest([Range(1, 50)] int Quantity = 1, string? Description = null);
public record CreateInvitesResponse(IReadOnlyList<string> Tokens);
public record InviteResponse(Guid Id, string Token, string? Description, DateTime ExpiresAt, DateTime CreatedAt, string Status);
