using FrogBets.Domain.Entities;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Services;

public class MatchStatsService : IMatchStatsService
{
    private readonly FrogBetsDbContext _db;

    public MatchStatsService(FrogBetsDbContext db)
    {
        _db = db;
    }

    public async Task<MatchStatsDto> RegisterStatsAsync(RegisterStatsRequest request)
    {
        if (request.KastPercent < 0 || request.KastPercent > 100)
            throw new InvalidOperationException("INVALID_KAST_VALUE");

        var playerExists = await _db.CS2Players.AnyAsync(p => p.Id == request.PlayerId);
        if (!playerExists)
            throw new InvalidOperationException("RESOURCE_NOT_FOUND");

        var mapResult = await _db.MapResults.FirstOrDefaultAsync(m => m.Id == request.MapResultId);
        if (mapResult is null)
            throw new InvalidOperationException("MAP_RESULT_NOT_FOUND");

        var duplicate = await _db.MatchStats.AnyAsync(s =>
            s.PlayerId == request.PlayerId && s.MapResultId == request.MapResultId);
        if (duplicate)
            throw new InvalidOperationException("STATS_ALREADY_REGISTERED");

        var rating = RatingCalculator.Calculate(
            request.Kills, request.Deaths, request.Assists,
            request.TotalDamage, mapResult.Rounds, request.KastPercent);

        var stats = new MatchStats
        {
            Id          = Guid.NewGuid(),
            PlayerId    = request.PlayerId,
            MapResultId = request.MapResultId,
            Kills       = request.Kills,
            Deaths      = request.Deaths,
            Assists     = request.Assists,
            TotalDamage = request.TotalDamage,
            KastPercent = request.KastPercent,
            Rating      = rating,
            CreatedAt   = DateTime.UtcNow,
        };

        _db.MatchStats.Add(stats);

        var player = await _db.CS2Players.FirstAsync(p => p.Id == request.PlayerId);
        player.PlayerScore  += rating;
        player.MatchesCount += 1;

        await _db.SaveChangesAsync();

        return new MatchStatsDto(
            stats.Id, stats.PlayerId, stats.MapResultId,
            mapResult.MapNumber, mapResult.Rounds,
            stats.Kills, stats.Deaths, stats.Assists,
            stats.TotalDamage, stats.KastPercent,
            stats.Rating, stats.CreatedAt);
    }

    public async Task<MatchStatsDto[]> GetStatsByPlayerAsync(Guid playerId)
    {
        return await _db.MatchStats
            .Where(s => s.PlayerId == playerId)
            .Join(_db.MapResults,
                s => s.MapResultId,
                m => m.Id,
                (s, m) => new MatchStatsDto(
                    s.Id, s.PlayerId, s.MapResultId,
                    m.MapNumber, m.Rounds,
                    s.Kills, s.Deaths, s.Assists,
                    s.TotalDamage, s.KastPercent,
                    s.Rating, s.CreatedAt))
            .ToArrayAsync();
    }
}
