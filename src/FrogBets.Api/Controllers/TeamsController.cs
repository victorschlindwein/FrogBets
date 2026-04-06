using System.Security.Claims;
using FrogBets.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrogBets.Api.Controllers;

[ApiController]
[Route("api/teams")]
public class TeamsController : ControllerBase
{
    private readonly ITeamService _teamService;

    public TeamsController(ITeamService teamService)
    {
        _teamService = teamService;
    }

    /// <summary>POST /api/teams — admin: create a new team.</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateTeam([FromBody] CreateTeamBody body)
    {
        if (!IsAdmin()) return Forbid();

        try
        {
            var team = await _teamService.CreateTeamAsync(new CreateTeamRequest(body.Name, body.LogoUrl));
            return CreatedAtAction(nameof(GetTeams), new { id = team.Id }, team);
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVALID_TEAM_NAME")
        {
            return BadRequest(new { error = new { code = ex.Message, message = "Nome do time inválido." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "TEAM_NAME_ALREADY_EXISTS")
        {
            return Conflict(new { error = new { code = ex.Message, message = "Já existe um time com esse nome." } });
        }
    }

    /// <summary>GET /api/teams — admin: list all teams.</summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetTeams()
    {
        if (!IsAdmin()) return Forbid();

        var teams = await _teamService.GetTeamsAsync();
        return Ok(teams);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private bool IsAdmin() =>
        User.FindFirstValue("isAdmin") == "true";
}

public record CreateTeamBody(string Name, string? LogoUrl);
