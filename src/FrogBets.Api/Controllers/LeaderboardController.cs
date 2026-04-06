using FrogBets.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Controllers;

[ApiController]
[Route("api/leaderboard")]
[Authorize]
public class LeaderboardController : ControllerBase
{
    private readonly FrogBetsDbContext _db;

    public LeaderboardController(FrogBetsDbContext db)
    {
        _db = db;
    }

    /// <summary>GET /api/leaderboard — returns all users ordered by VirtualBalance descending.</summary>
    [HttpGet]
    public async Task<IActionResult> GetLeaderboard()
    {
        var entries = await _db.Users
            .AsNoTracking()
            .OrderByDescending(u => u.VirtualBalance)
            .Select(u => new
            {
                username       = u.Username,
                virtualBalance = u.VirtualBalance,
                winsCount      = u.WinsCount,
                lossesCount    = u.LossesCount,
            })
            .ToListAsync();

        return Ok(entries);
    }
}
