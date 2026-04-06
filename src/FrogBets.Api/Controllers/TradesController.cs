using System.Security.Claims;
using FrogBets.Api.Services;
using FrogBets.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Controllers;

[ApiController]
[Route("api/trades")]
[Authorize]
public class TradesController : ControllerBase
{
    private readonly ITradeService _tradeService;
    private readonly FrogBetsDbContext _db;

    public TradesController(ITradeService tradeService, FrogBetsDbContext db)
    {
        _tradeService = tradeService;
        _db = db;
    }

    /// <summary>GET /api/trades/listings — any authenticated user: list all trade listings.</summary>
    [HttpGet("listings")]
    public async Task<IActionResult> GetListings()
    {
        var listings = await _tradeService.GetListingsAsync();
        return Ok(listings);
    }

    /// <summary>POST /api/trades/listings — teamLeader or admin: add a user to the trade listing.</summary>
    [HttpPost("listings")]
    public async Task<IActionResult> AddListing([FromBody] AddListingBody body)
    {
        var requesterId = GetCurrentUserId();
        if (requesterId is null) return Unauthorized();

        if (!IsAdmin() && !await IsTeamLeaderAsync(requesterId.Value))
            return StatusCode(403, new { error = new { code = "FORBIDDEN", message = "Acesso negado." } });

        try
        {
            await _tradeService.AddListingAsync(requesterId.Value, body.UserId);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "FORBIDDEN")
        {
            return StatusCode(403, new { error = new { code = ex.Message, message = "Acesso negado." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "ALREADY_LISTED")
        {
            return Conflict(new { error = new { code = ex.Message, message = "Usuário já está na lista de trocas." } });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = new { code = "USER_NOT_FOUND", message = ex.Message } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "USER_NOT_FOUND")
        {
            return NotFound(new { error = new { code = ex.Message, message = "Usuário não encontrado." } });
        }
    }

    /// <summary>DELETE /api/trades/listings/{userId} — teamLeader or admin: remove a user from the trade listing.</summary>
    [HttpDelete("listings/{userId:guid}")]
    public async Task<IActionResult> RemoveListing(Guid userId)
    {
        var requesterId = GetCurrentUserId();
        if (requesterId is null) return Unauthorized();

        if (!IsAdmin() && !await IsTeamLeaderAsync(requesterId.Value))
            return StatusCode(403, new { error = new { code = "FORBIDDEN", message = "Acesso negado." } });

        try
        {
            await _tradeService.RemoveListingAsync(requesterId.Value, userId);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "FORBIDDEN")
        {
            return StatusCode(403, new { error = new { code = ex.Message, message = "Acesso negado." } });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = new { code = "USER_NOT_FOUND", message = ex.Message } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "USER_NOT_FOUND")
        {
            return NotFound(new { error = new { code = ex.Message, message = "Usuário não encontrado." } });
        }
    }

    /// <summary>GET /api/trades/offers — teamLeader: get received trade offers.</summary>
    [HttpGet("offers")]
    public async Task<IActionResult> GetOffers()
    {
        var requesterId = GetCurrentUserId();
        if (requesterId is null) return Unauthorized();

        if (!await IsTeamLeaderAsync(requesterId.Value))
            return StatusCode(403, new { error = new { code = "FORBIDDEN", message = "Acesso negado." } });

        var offers = await _tradeService.GetReceivedOffersAsync(requesterId.Value);
        return Ok(offers);
    }

    /// <summary>POST /api/trades/offers — teamLeader or admin: create a trade offer.</summary>
    [HttpPost("offers")]
    public async Task<IActionResult> CreateOffer([FromBody] CreateOfferBody body)
    {
        var requesterId = GetCurrentUserId();
        if (requesterId is null) return Unauthorized();

        if (!IsAdmin() && !await IsTeamLeaderAsync(requesterId.Value))
            return StatusCode(403, new { error = new { code = "FORBIDDEN", message = "Acesso negado." } });

        try
        {
            var offerId = await _tradeService.CreateOfferAsync(requesterId.Value, body.OfferedUserId, body.TargetUserId);
            return StatusCode(201, new { id = offerId });
        }
        catch (InvalidOperationException ex) when (ex.Message == "FORBIDDEN")
        {
            return StatusCode(403, new { error = new { code = ex.Message, message = "Acesso negado." } });
        }
        catch (InvalidOperationException ex) when (ex.Message is "TARGET_NOT_AVAILABLE" or "SAME_TEAM_TRADE")
        {
            var message = ex.Message switch
            {
                "TARGET_NOT_AVAILABLE" => "O usuário alvo não está disponível para troca.",
                "SAME_TEAM_TRADE"      => "Não é possível trocar jogadores do mesmo time.",
                _                      => ex.Message,
            };
            return BadRequest(new { error = new { code = ex.Message, message } });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = new { code = "USER_NOT_FOUND", message = ex.Message } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "USER_NOT_FOUND")
        {
            return NotFound(new { error = new { code = ex.Message, message = "Usuário não encontrado." } });
        }
    }

    /// <summary>PATCH /api/trades/offers/{id}/accept — teamLeader: accept a trade offer.</summary>
    [HttpPatch("offers/{id:guid}/accept")]
    public async Task<IActionResult> AcceptOffer(Guid id)
    {
        var requesterId = GetCurrentUserId();
        if (requesterId is null) return Unauthorized();

        if (!await IsTeamLeaderAsync(requesterId.Value))
            return StatusCode(403, new { error = new { code = "FORBIDDEN", message = "Acesso negado." } });

        try
        {
            await _tradeService.AcceptOfferAsync(requesterId.Value, id);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "FORBIDDEN")
        {
            return StatusCode(403, new { error = new { code = ex.Message, message = "Acesso negado." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "OFFER_NOT_PENDING")
        {
            return BadRequest(new { error = new { code = ex.Message, message = "A oferta não está pendente." } });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
        }
    }

    /// <summary>PATCH /api/trades/offers/{id}/reject — teamLeader: reject a trade offer.</summary>
    [HttpPatch("offers/{id:guid}/reject")]
    public async Task<IActionResult> RejectOffer(Guid id)
    {
        var requesterId = GetCurrentUserId();
        if (requesterId is null) return Unauthorized();

        if (!await IsTeamLeaderAsync(requesterId.Value))
            return StatusCode(403, new { error = new { code = "FORBIDDEN", message = "Acesso negado." } });

        try
        {
            await _tradeService.RejectOfferAsync(requesterId.Value, id);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "FORBIDDEN")
        {
            return StatusCode(403, new { error = new { code = ex.Message, message = "Acesso negado." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "OFFER_NOT_PENDING")
        {
            return BadRequest(new { error = new { code = ex.Message, message = "A oferta não está pendente." } });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
        }
    }

    /// <summary>POST /api/trades/direct — admin: directly swap two users between teams.</summary>
    [HttpPost("direct")]
    public async Task<IActionResult> DirectSwap([FromBody] DirectSwapBody body)
    {
        if (!IsAdmin())
            return StatusCode(403, new { error = new { code = "FORBIDDEN", message = "Acesso negado." } });

        try
        {
            await _tradeService.DirectSwapAsync(body.UserAId, body.UserBId);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "SAME_TEAM_TRADE")
        {
            return BadRequest(new { error = new { code = ex.Message, message = "Não é possível trocar jogadores do mesmo time." } });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = new { code = "USER_NOT_FOUND", message = ex.Message } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "USER_NOT_FOUND")
        {
            return NotFound(new { error = new { code = ex.Message, message = "Usuário não encontrado." } });
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private bool IsAdmin() =>
        User.FindFirstValue("isAdmin") == "true";

    private async Task<bool> IsTeamLeaderAsync(Guid userId) =>
        await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.IsTeamLeader)
            .FirstOrDefaultAsync();
}

public record AddListingBody(Guid UserId);
public record CreateOfferBody(Guid OfferedUserId, Guid TargetUserId);
public record DirectSwapBody(Guid UserAId, Guid UserBId);
