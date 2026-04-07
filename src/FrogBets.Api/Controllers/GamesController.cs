using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FrogBets.Api.Services;
using FrogBets.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Controllers;

[ApiController]
[Route("api/games")]
public class GamesController : ControllerBase
{
    private readonly IGameService _gameService;
    private readonly FrogBetsDbContext _db;

    public GamesController(IGameService gameService, FrogBetsDbContext db)
    {
        _gameService = gameService;
        _db = db;
    }

    /// <summary>GET /api/games — public listing of all games.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetGames()
    {
        var games = await _gameService.GetGamesAsync();
        return Ok(games);
    }

    /// <summary>GET /api/games/{id} — public: get a single game with its markets.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetGame(Guid id)
    {
        var game = await _gameService.GetGameByIdAsync(id);
        if (game is null)
            return NotFound(new { error = new { code = "GAME_NOT_FOUND", message = "Jogo não encontrado." } });
        return Ok(game);
    }

    /// <summary>POST /api/games — admin: create a new game with auto-generated markets.</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateGame([FromBody] CreateGameBody body)
    {
        if (!await IsAdminFromDb()) return Forbid();

        var gameId = await _gameService.CreateGameAsync(
            new CreateGameRequest(body.TeamA, body.TeamB, body.ScheduledAt, body.NumberOfMaps));

        return CreatedAtAction(nameof(GetGames), new { id = gameId }, new { id = gameId });
    }

    /// <summary>PATCH /api/games/{id}/start — admin: start game, close all open markets.</summary>
    [HttpPatch("{id:guid}/start")]
    [Authorize]
    public async Task<IActionResult> StartGame(Guid id)
    {
        if (!await IsAdminFromDb()) return Forbid();

        try
        {
            await _gameService.StartGameAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = new { code = "GAME_NOT_FOUND", message = ex.Message } });
        }
    }

    /// <summary>POST /api/games/{id}/results — admin: register a market result.</summary>
    [HttpPost("{id:guid}/results")]
    [Authorize]
    public async Task<IActionResult> RegisterResult(Guid id, [FromBody] RegisterResultBody body)
    {
        if (!await IsAdminFromDb()) return Forbid();

        var adminId = GetCurrentUserId();
        if (adminId is null) return Unauthorized();

        try
        {
            await _gameService.RegisterResultAsync(
                id,
                new RegisterResultRequest(body.MarketId, body.WinningOption, body.MapNumber),
                adminId.Value);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "GAME_ALREADY_FINISHED")
        {
            return Conflict(new
            {
                error = new
                {
                    code    = "GAME_ALREADY_FINISHED",
                    message = "O jogo já foi finalizado e não aceita novos resultados.",
                }
            });
        }
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

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}

public record CreateGameBody(
    [Required][StringLength(100, MinimumLength = 1)] string TeamA,
    [Required][StringLength(100, MinimumLength = 1)] string TeamB,
    [Required] DateTime ScheduledAt,
    [Range(1, 5)] int NumberOfMaps
);
public record RegisterResultBody([Required] Guid MarketId, [Required][StringLength(200, MinimumLength = 1)] string WinningOption, int? MapNumber);
