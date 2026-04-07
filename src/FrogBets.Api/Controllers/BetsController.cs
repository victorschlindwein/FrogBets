using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FrogBets.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrogBets.Api.Controllers;

[ApiController]
[Route("api/bets")]
[Authorize]
public class BetsController : ControllerBase
{
    private readonly IBetService _betService;

    public BetsController(IBetService betService)
    {
        _betService = betService;
    }

    /// <summary>GET /api/bets — list all bets for the authenticated user.</summary>
    [HttpGet]
    public async Task<IActionResult> GetUserBets()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var bets = await _betService.GetUserBetsAsync(userId.Value);
        return Ok(bets);
    }

    /// <summary>POST /api/bets — create a new bet on a market.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateBet([FromBody] CreateBetBody body)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var betId = await _betService.CreateBetAsync(
                userId.Value, body.MarketId, body.CreatorOption, body.Amount);

            return StatusCode(201, new
            {
                id            = betId,
                marketId      = body.MarketId,
                creatorOption = body.CreatorOption,
                amount        = body.Amount,
                status        = "Pending",
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
        }
        catch (InvalidOperationException ex) when (ex.Message is
            "MARKET_NOT_OPEN" or
            "GAME_ALREADY_STARTED" or
            "DUPLICATE_BET_ON_MARKET" or
            "INSUFFICIENT_BALANCE" or
            "INVALID_BET_OPTION")
        {
            var message = ex.Message switch
            {
                "MARKET_NOT_OPEN"          => "O mercado não está aberto para apostas.",
                "GAME_ALREADY_STARTED"     => "O jogo já foi iniciado e não aceita novas apostas.",
                "DUPLICATE_BET_ON_MARKET"  => "Você já possui uma aposta neste mercado.",
                "INSUFFICIENT_BALANCE"     => "Saldo Virtual insuficiente para realizar esta aposta.",
                "INVALID_BET_OPTION"       => "Opção de aposta inválida para este tipo de mercado.",
                _                          => ex.Message,
            };

            return BadRequest(new { error = new { code = ex.Message, message } });
        }
    }

    /// <summary>POST /api/bets/{id}/cover — cover an existing pending bet.</summary>
    [HttpPost("{id}/cover")]
    public async Task<IActionResult> CoverBet(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            await _betService.CoverBetAsync(userId.Value, id);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
        }
        catch (InvalidOperationException ex) when (ex.Message is
            "CANNOT_COVER_OWN_BET" or
            "BET_NOT_AVAILABLE" or
            "INSUFFICIENT_BALANCE")
        {
            var message = ex.Message switch
            {
                "CANNOT_COVER_OWN_BET"  => "Você não pode cobrir a própria aposta.",
                "BET_NOT_AVAILABLE"     => "Esta aposta não está mais disponível para cobertura.",
                "INSUFFICIENT_BALANCE"  => "Saldo Virtual insuficiente para cobrir esta aposta.",
                _                       => ex.Message,
            };

            return BadRequest(new { error = new { code = ex.Message, message } });
        }
    }

    /// <summary>DELETE /api/bets/{id} — cancel a pending bet.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> CancelBet(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            await _betService.CancelBetAsync(userId.Value, id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
        }
        catch (InvalidOperationException ex) when (ex.Message is
            "NOT_BET_OWNER" or
            "CANNOT_CANCEL_ACTIVE_BET")
        {
            var message = ex.Message switch
            {
                "NOT_BET_OWNER"              => "Você não é o criador desta aposta.",
                "CANNOT_CANCEL_ACTIVE_BET"   => "Apostas ativas não podem ser canceladas.",
                _                            => ex.Message,
            };

            return BadRequest(new { error = new { code = ex.Message, message } });
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

public record CreateBetBody(
    [Required] Guid MarketId,
    [Required][StringLength(200, MinimumLength = 1)] string CreatorOption,
    [Range(0.01, 1_000_000)] decimal Amount
);
