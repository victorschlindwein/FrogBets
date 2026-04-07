using System.Security.Claims;
using FrogBets.Api.Services;
using FrogBets.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Controllers;

[ApiController]
[Route("api/players")]
public class PlayersController : ControllerBase
{
    private readonly IPlayerService _playerService;
    private readonly IMatchStatsService _matchStatsService;
    private readonly FrogBetsDbContext _db;

    public PlayersController(IPlayerService playerService, IMatchStatsService matchStatsService, FrogBetsDbContext db)
    {
        _playerService = playerService;
        _matchStatsService = matchStatsService;
        _db = db;
    }

    /// <summary>POST /api/players — admin: create a new player.</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreatePlayer([FromBody] CreatePlayerBody body)
    {
        if (!await IsAdminFromDb()) return Forbid();

        try
        {
            var player = await _playerService.CreatePlayerAsync(
                new CreatePlayerRequest(body.Nickname, body.RealName, body.TeamId, body.PhotoUrl));
            return CreatedAtAction(nameof(GetPlayers), new { id = player.Id }, player);
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVALID_PLAYER_DATA")
        {
            return BadRequest(new { error = new { code = ex.Message, message = "Dados do jogador inválidos." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "TEAM_NOT_FOUND")
        {
            return NotFound(new { error = new { code = ex.Message, message = "Time não encontrado." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "PLAYER_NICKNAME_ALREADY_EXISTS")
        {
            return Conflict(new { error = new { code = ex.Message, message = "Já existe um jogador com esse nickname." } });
        }
    }

    /// <summary>GET /api/players — admin: list all players.</summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetPlayers()
    {
        if (!await IsAdminFromDb()) return Forbid();

        var players = await _playerService.GetPlayersAsync();
        return Ok(players);
    }

    /// <summary>GET /api/players/ranking — public: player ranking.</summary>
    [HttpGet("ranking")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRanking()
    {
        var ranking = await _playerService.GetRankingAsync();
        return Ok(ranking);
    }

    /// <summary>POST /api/players/{id}/stats — admin: register match stats for a player.</summary>
    [HttpPost("{id:guid}/stats")]
    [Authorize]
    public async Task<IActionResult> RegisterStats(Guid id, [FromBody] RegisterStatsBody body)
    {
        if (!await IsAdminFromDb()) return Forbid();

        try
        {
            var stats = await _matchStatsService.RegisterStatsAsync(new RegisterStatsRequest(
                PlayerId: id,
                MapResultId: body.MapResultId,
                Kills: body.Kills,
                Deaths: body.Deaths,
                Assists: body.Assists,
                TotalDamage: body.TotalDamage,
                KastPercent: body.KastPercent));

            return StatusCode(201, stats);
        }
        catch (InvalidOperationException ex) when (ex.Message == "MAP_RESULT_NOT_FOUND")
        {
            return NotFound(new { error = new { code = ex.Message, message = "MapResult não encontrado." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVALID_KAST_VALUE")
        {
            return BadRequest(new { error = new { code = ex.Message, message = "Valor de KAST inválido." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "RESOURCE_NOT_FOUND")
        {
            return NotFound(new { error = new { code = ex.Message, message = "Recurso não encontrado." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "STATS_ALREADY_REGISTERED")
        {
            return Conflict(new { error = new { code = ex.Message, message = "Estatísticas já registradas para este jogador neste mapa." } });
        }
    }

    /// <summary>GET /api/players/{id}/stats — public: get stats for a player.</summary>
    [HttpGet("{id:guid}/stats")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStats(Guid id)
    {
        var stats = await _matchStatsService.GetStatsByPlayerAsync(id);
        return Ok(stats);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private bool IsAdmin() =>
        User.FindFirstValue("isAdmin") == "true";

    private async Task<bool> IsAdminFromDb()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId)) return false;
        return await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsAdmin);
    }
}

public record CreatePlayerBody(string Nickname, string? RealName, Guid TeamId, string? PhotoUrl);
public record RegisterStatsBody(Guid MapResultId, int Kills, int Deaths, int Assists,
    double TotalDamage, double KastPercent);
