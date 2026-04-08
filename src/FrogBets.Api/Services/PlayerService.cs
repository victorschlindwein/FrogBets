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

    public async Task<CS2PlayerDto> CreatePlayerAsync(Guid userId, Guid teamId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("USER_NOT_FOUND");

        var teamExists = await _db.CS2Teams.AnyAsync(t => t.Id == teamId && !t.IsDeleted);
        if (!teamExists)
            throw new InvalidOperationException("TEAM_NOT_FOUND");

        var nicknameExists = await _db.CS2Players.AnyAsync(p => p.Nickname == user.Username);
        if (nicknameExists)
            throw new InvalidOperationException("NICKNAME_TAKEN");

        var player = new CS2Player
        {
            Id           = Guid.NewGuid(),
            UserId       = userId,
            Nickname     = user.Username,
            TeamId       = teamId,
            PlayerScore  = 0.0,
            MatchesCount = 0,
            CreatedAt    = DateTime.UtcNow,
        };

        user.TeamId = teamId;

        _db.CS2Players.Add(player);
        await _db.SaveChangesAsync();

        await _db.Entry(player).Reference(p => p.Team).LoadAsync();
        return ToDto(player);
    }

    public async Task<CS2PlayerDto> AssignTeamAsync(Guid playerId, Guid teamId)
    {
        var player = await _db.CS2Players
            .Include(p => p.Team)
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == playerId)
            ?? throw new KeyNotFoundException("PLAYER_NOT_FOUND");

        var team = await _db.CS2Teams.FirstOrDefaultAsync(t => t.Id == teamId && !t.IsDeleted)
            ?? throw new InvalidOperationException("TEAM_NOT_FOUND");

        player.TeamId = teamId;
        player.Team   = team;

        if (player.UserId.HasValue)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == player.UserId.Value);
            if (user is not null) user.TeamId = teamId;
        }

        await _db.SaveChangesAsync();
        return ToDto(player);
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
                TeamName:    p.Team?.Name ?? "—",
                PlayerScore: p.PlayerScore,
                MatchesCount: p.MatchesCount))
            .ToList();
    }

    private static CS2PlayerDto ToDto(CS2Player p) =>
        new(p.Id, p.Nickname, p.RealName, p.TeamId, p.Team?.Name,
            p.PhotoUrl, p.PlayerScore, p.MatchesCount, p.CreatedAt, p.User?.Username);
}
