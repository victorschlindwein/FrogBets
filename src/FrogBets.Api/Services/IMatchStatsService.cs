namespace FrogBets.Api.Services;

public record RegisterStatsRequest(
    Guid PlayerId, Guid MapResultId,
    int Kills, int Deaths, int Assists,
    double TotalDamage, double KastPercent);

public record MatchStatsDto(
    Guid Id, Guid PlayerId, Guid MapResultId,
    int MapNumber, int Rounds,
    int Kills, int Deaths, int Assists,
    double TotalDamage, double KastPercent, double Rating, DateTime CreatedAt);

public interface IMatchStatsService
{
    Task<MatchStatsDto> RegisterStatsAsync(RegisterStatsRequest request);
    Task<MatchStatsDto[]> GetStatsByPlayerAsync(Guid playerId);
}
