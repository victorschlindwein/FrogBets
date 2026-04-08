using FrogBets.Domain.Entities;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Services;

public class PlayerService : IPlayerService
{
    private readonly FrogBetsDbContext _db;

    public PlayerService(FrogBetsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CS2PlayerDto>> GetPlayersAsync()
    {
        var players = await _db.CS2Players
            .Include(p => p.Team)
            .Include(p => p.User)
            .AsNoTracking()
            .OrderBy(p => p.Nickname)
            .ToListAsync();

        return players.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<PlayerRankingItemDto>> GetRankingAsync()
    {
        var players = await _db.CS2Players
            .Include(p => p.Team)
            .AsNoTracking()
            .OrderByDescending(p => p.PlayerScore)
            .ToListAsync();

        return players
            .Select((p, i) => new PlayerRankingItemDto(
                Position:    i + 1,
                PlayerId:    p.Id,
                Nickname:    p.Nickname,
                TeamName:    p.Team.Name,
                PlayerScore: p.PlayerScore,
                MatchesCount: p.MatchesCount))
            .ToList();
    }

    private static CS2PlayerDto ToDto(CS2Player p) =>
        new(p.Id, p.Nickname, p.RealName, p.TeamId, p.Team.Name,
            p.PhotoUrl, p.PlayerScore, p.MatchesCount, p.CreatedAt, p.User?.Username);
}
