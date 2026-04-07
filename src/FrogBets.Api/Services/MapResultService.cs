using FrogBets.Domain.Entities;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Services;

public class MapResultService : IMapResultService
{
    private readonly FrogBetsDbContext _db;

    public MapResultService(FrogBetsDbContext db)
    {
        _db = db;
    }

    public async Task<MapResultDto> CreateMapResultAsync(CreateMapResultRequest request)
    {
        if (request.MapNumber < 1)
            throw new InvalidOperationException("INVALID_MAP_NUMBER");

        if (request.Rounds <= 0)
            throw new InvalidOperationException("INVALID_ROUNDS_COUNT");

        var gameExists = await _db.Games.AnyAsync(g => g.Id == request.GameId);
        if (!gameExists)
            throw new KeyNotFoundException("MAP_GAME_NOT_FOUND");

        var mapResult = new MapResult
        {
            Id        = Guid.NewGuid(),
            GameId    = request.GameId,
            MapNumber = request.MapNumber,
            Rounds    = request.Rounds,
            CreatedAt = DateTime.UtcNow,
        };

        _db.MapResults.Add(mapResult);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new InvalidOperationException("MAP_ALREADY_REGISTERED");
        }

        return new MapResultDto(mapResult.Id, mapResult.GameId, mapResult.MapNumber, mapResult.Rounds, mapResult.CreatedAt);
    }

    public async Task<IReadOnlyList<MapResultDto>> GetByGameAsync(Guid gameId)
    {
        return await _db.MapResults
            .Where(m => m.GameId == gameId)
            .OrderBy(m => m.MapNumber)
            .Select(m => new MapResultDto(m.Id, m.GameId, m.MapNumber, m.Rounds, m.CreatedAt))
            .ToListAsync();
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true
            || ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true
            || ex.InnerException?.Message.Contains("23505") == true; // PostgreSQL unique violation code
    }
}
