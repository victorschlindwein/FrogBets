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
        if (request.Rounds <= 0)
            throw new InvalidOperationException("INVALID_ROUNDS_COUNT");

        if (request.KastPercent < 0 || request.KastPercent > 100)
            throw new InvalidOperationException("INVALID_KAST_VALUE");

        var playerExists = await _db.CS2Players.AnyAsync(p => p.Id == request.PlayerId);
        if (!playerExists)
            throw new InvalidOperationException("RESOURCE_NOT_FOUND");

        var gameExists = await _db.Games.AnyAsync(g => g.Id == request.GameId);
        if (!gameExists)
            throw new InvalidOperationException("RESOURCE_NOT_FOUND");

        var duplicate = await _db.MatchStats.AnyAsync(s => s.PlayerId == request.PlayerId && s.GameId == request.GameId);
        if (duplicate)
            throw new InvalidOperationException("STATS_ALREADY_REGISTERED");

        var rating = RatingCalculator.Calculate(
            request.Kills, request.Deaths, request.Assists,
            request.TotalDamage, request.Rounds, request.KastPercent);

        var stats = new MatchStats
        {
            Id          = Guid.NewGuid(),
            PlayerId    = request.PlayerId,
            GameId      = request.GameId,
            Kills       = request.Kills,
            Deaths      = request.Deaths,
            Assists     = request.Assists,
            TotalDamage = request.TotalDamage,
            Rounds      = request.Rounds,
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
            stats.Id, stats.PlayerId, stats.GameId,
            stats.Kills, stats.Deaths, stats.Assists,
            stats.TotalDamage, stats.Rounds, stats.KastPercent,
            stats.Rating, stats.CreatedAt);
    }
}
