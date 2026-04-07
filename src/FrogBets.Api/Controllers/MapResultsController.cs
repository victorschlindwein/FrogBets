using System.Security.Claims;
using FrogBets.Api.Services;
using FrogBets.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Controllers;

[ApiController]
[Route("api/map-results")]
public class MapResultsController : ControllerBase
{
    private readonly IMapResultService _mapResultService;
    private readonly FrogBetsDbContext _db;

    public MapResultsController(IMapResultService mapResultService, FrogBetsDbContext db)
    {
        _mapResultService = mapResultService;
        _db = db;
    }

    /// <summary>POST /api/map-results — admin: register a map result for a game.</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateMapResult([FromBody] CreateMapResultBody body)
    {
        if (!await IsAdminFromDb()) return Forbid();

        try
        {
            var mapResult = await _mapResultService.CreateMapResultAsync(
                new CreateMapResultRequest(body.GameId, body.MapNumber, body.Rounds));

            return StatusCode(201, mapResult);
        }
        catch (KeyNotFoundException ex) when (ex.Message == "MAP_GAME_NOT_FOUND")
        {
            return NotFound(new { error = new { code = "MAP_GAME_NOT_FOUND", message = "Jogo não encontrado." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVALID_MAP_NUMBER")
        {
            return BadRequest(new { error = new { code = "INVALID_MAP_NUMBER", message = "Número do mapa inválido." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVALID_ROUNDS_COUNT")
        {
            return BadRequest(new { error = new { code = "INVALID_ROUNDS_COUNT", message = "Número de rounds inválido." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "MAP_ALREADY_REGISTERED")
        {
            return Conflict(new { error = new { code = "MAP_ALREADY_REGISTERED", message = "Este mapa já foi registrado para este jogo." } });
        }
    }

    /// <summary>GET /api/map-results?gameId= — admin: list map results for a game, ordered by MapNumber.</summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetMapResults([FromQuery] Guid gameId)
    {
        if (!await IsAdminFromDb()) return Forbid();

        var results = await _mapResultService.GetByGameAsync(gameId);
        return Ok(results);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<bool> IsAdminFromDb()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId)) return false;
        return await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsAdmin);
    }
}

public record CreateMapResultBody(Guid GameId, int MapNumber, int Rounds);
