using System.Security.Claims;
using FrogBets.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrogBets.Api.Controllers;

[ApiController]
[Route("api/players")]
public class PlayersController : ControllerBase
{
    private readonly IPlayerService _playerService;
    private readonly IMatchStatsService _matchStatsService;

    public PlayersController(IPlayerService playerService, IMatchStatsService matchStatsService)
    {
        _playerService = playerService;
        _matchStatsService = matchStatsService;
    }

    /// <summary>POST /api/players — admin: create a new player.</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreatePlayer([FromBody] CreatePlayerBody body)
    {
        if (!IsAdmin()) return Forbid();

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
        if (!IsAdmin()) return Forbid();

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
        if (!IsAdmin()) return Forbid();

        try
        {
            var stats = await _matchStatsService.RegisterStatsAsync(new RegisterStatsRequest(
                PlayerId: id,
                GameId: body.GameId,
                Kills: body.Kills,
                Deaths: body.Deaths,
                Assists: body.Assists,
                TotalDamage: body.TotalDamage,
                Rounds: body.Rounds,
                KastPercent: body.KastPercent));

            return StatusCode(201, stats);
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVALID_ROUNDS_COUNT")
        {
            return BadRequest(new { error = new { code = ex.Message, message = "Número de rounds inválido." } });
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
            return Conflict(new { error = new { code = ex.Message, message = "Estatísticas já registradas para este jogador neste jogo." } });
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private bool IsAdmin() =>
        User.FindFirstValue("isAdmin") == "true";
}

public record CreatePlayerBody(string Nickname, string? RealName, Guid TeamId, string? PhotoUrl);
public record RegisterStatsBody(Guid GameId, int Kills, int Deaths, int Assists,
    double TotalDamage, int Rounds, double KastPercent);
