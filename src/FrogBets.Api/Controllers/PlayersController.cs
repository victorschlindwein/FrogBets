using System.ComponentModel.DataAnnotations;
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

    /// <summary>GET /api/players — admin: list all players.</summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetPlayers()
    {
        if (!await IsAdminFromDb()) return Forbid();

        var players = await _playerService.GetPlayersAsync();
        return Ok(players);
    }

    /// <summary>POST /api/players — admin: create a new CS2 player from an existing user.</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreatePlayer([FromBody] CreatePlayerBody body)
    {
        if (!await IsAdminFromDb()) return Forbid();

        try
        {
            var player = await _playerService.CreatePlayerAsync(body.UserId, body.TeamId);
            return StatusCode(201, player);
        }
        catch (KeyNotFoundException ex) when (ex.Message == "USER_NOT_FOUND")
            => NotFound(new { error = new { code = ex.Message, message = "Usuário não encontrado." } });
        catch (InvalidOperationException ex) when (ex.Message == "TEAM_NOT_FOUND")
            => NotFound(new { error = new { code = ex.Message, message = "Time não encontrado." } });
        catch (InvalidOperationException ex) when (ex.Message == "NICKNAME_TAKEN")
            => Conflict(new { error = new { code = ex.Message, message = "Já existe um jogador com esse username." } });
    }

    /// <summary>PATCH /api/players/{id}/team — admin: assign a player to a team.</summary>
    [HttpPatch("{id:guid}/team")]
    [Authorize]
    public async Task<IActionResult> AssignTeam(Guid id, [FromBody] AssignTeamBody body)
    {
        if (!await IsAdminFromDb()) return Forbid();

        try
        {
            var player = await _playerService.AssignTeamAsync(id, body.TeamId);
            return Ok(player);
        }
        catch (KeyNotFoundException ex) when (ex.Message == "PLAYER_NOT_FOUND")
            => NotFound(new { error = new { code = ex.Message, message = "Jogador não encontrado." } });
        catch (InvalidOperationException ex) when (ex.Message == "TEAM_NOT_FOUND")
            => NotFound(new { error = new { code = ex.Message, message = "Time não encontrado." } });
    }

    /// <summary>GET /api/players/ranking — authenticated: player ranking.</summary>
    [HttpGet("ranking")]
    [Authorize]
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

    /// <summary>GET /api/players/{id}/stats — authenticated: get stats for a player.</summary>
    [HttpGet("{id:guid}/stats")]
    [Authorize]
    public async Task<IActionResult> GetStats(Guid id)
    {
        var stats = await _matchStatsService.GetStatsByPlayerAsync(id);
        return Ok(stats);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<bool> IsAdminFromDb()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId)) return false;
        return await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsAdmin);
    }
}

public record RegisterStatsBody(Guid MapResultId, int Kills, int Deaths, int Assists,
    double TotalDamage, double KastPercent);
public record CreatePlayerBody([Required] Guid UserId, [Required] Guid TeamId);
public record AssignTeamBody([Required] Guid TeamId);
