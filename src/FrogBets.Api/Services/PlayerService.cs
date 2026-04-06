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

    public async Task<CS2PlayerDto> CreatePlayerAsync(CreatePlayerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nickname))
            throw new InvalidOperationException("INVALID_PLAYER_DATA");

        var teamExists = await _db.CS2Teams.AnyAsync(t => t.Id == request.TeamId);
        if (!teamExists)
            throw new InvalidOperationException("TEAM_NOT_FOUND");

        var nicknameExists = await _db.CS2Players.AnyAsync(p => p.Nickname == request.Nickname);
        if (nicknameExists)
            throw new InvalidOperationException("PLAYER_NICKNAME_ALREADY_EXISTS");

        var player = new CS2Player
        {
            Id           = Guid.NewGuid(),
            Nickname     = request.Nickname,
            RealName     = request.RealName,
            TeamId       = request.TeamId,
            PhotoUrl     = request.PhotoUrl,
            PlayerScore  = 0.0,
            MatchesCount = 0,
            CreatedAt    = DateTime.UtcNow,
        };

        _db.CS2Players.Add(player);
        await _db.SaveChangesAsync();

        var created = await _db.CS2Players
            .Include(p => p.Team)
            .AsNoTracking()
            .FirstAsync(p => p.Id == player.Id);

        return ToDto(created);
    }

    public async Task<IReadOnlyList<CS2PlayerDto>> GetPlayersAsync()
    {
        var players = await _db.CS2Players
            .Include(p => p.Team)
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
            p.PhotoUrl, p.PlayerScore, p.MatchesCount, p.CreatedAt);
}
