using System.Security.Claims;
using FrogBets.Api.Services;
using FrogBets.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Controllers;

[ApiController]
[Route("api/teams")]
public class TeamsController : ControllerBase
{
    private readonly ITeamService _teamService;
    private readonly ITeamMembershipService _teamMembershipService;
    private readonly FrogBetsDbContext _db;

    public TeamsController(ITeamService teamService, ITeamMembershipService teamMembershipService, FrogBetsDbContext db)
    {
        _teamService = teamService;
        _teamMembershipService = teamMembershipService;
        _db = db;
    }

    /// <summary>POST /api/teams — admin: create a new team.</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateTeam([FromBody] CreateTeamBody body)
    {
        if (!await IsAdminFromDb()) return Forbid();

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

    /// <summary>GET /api/teams — lista todos os times (público).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetTeams()
    {
        var teams = await _teamService.GetTeamsAsync();
        return Ok(teams);
    }

    /// <summary>POST /api/teams/{teamId}/leader/{userId} — admin: assign a team leader.</summary>
    [HttpPost("{teamId:guid}/leader/{userId:guid}")]
    [Authorize]
    public async Task<IActionResult> AssignLeader(Guid teamId, Guid userId)
    {
        if (!await IsAdminFromDb()) return Forbid();

        try
        {
            await _teamMembershipService.AssignLeaderAsync(teamId, userId);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "TEAM_NOT_FOUND")
        {
            return NotFound(new { error = new { code = ex.Message, message = "Time não encontrado." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "USER_NOT_FOUND")
        {
            return NotFound(new { error = new { code = ex.Message, message = "Usuário não encontrado." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "USER_NOT_IN_TEAM")
        {
            return Conflict(new { error = new { code = ex.Message, message = "O usuário não pertence a este time." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "ALREADY_LEADER_OF_OTHER_TEAM")
        {
            return Conflict(new { error = new { code = ex.Message, message = "O usuário já é líder de outro time." } });
        }
    }

    /// <summary>DELETE /api/teams/{teamId}/leader — admin: remove the team leader.</summary>
    [HttpDelete("{teamId:guid}/leader")]
    [Authorize]
    public async Task<IActionResult> RemoveLeader(Guid teamId)
    {
        if (!await IsAdminFromDb()) return Forbid();

        try
        {
            await _teamMembershipService.RemoveLeaderAsync(teamId);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "TEAM_NOT_FOUND")
        {
            return NotFound(new { error = new { code = ex.Message, message = "Time não encontrado." } });
        }
    }

    /// <summary>DELETE /api/teams/{teamId} — admin: soft-delete a team.</summary>
    [HttpDelete("{teamId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteTeam(Guid teamId)
    {
        if (!await IsAdminFromDb()) return Forbid();

        try
        {
            await _teamService.DeleteTeamAsync(teamId);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "TEAM_NOT_FOUND")
        {
            return NotFound(new { error = new { code = ex.Message, message = "Time não encontrado." } });
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<bool> IsAdminFromDb()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId)) return false;
        return await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsAdmin);
    }
}

public record CreateTeamBody(string Name, string? LogoUrl);
