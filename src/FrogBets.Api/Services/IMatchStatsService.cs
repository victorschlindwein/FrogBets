namespace FrogBets.Api.Services;

public record RegisterStatsRequest(
    Guid PlayerId, Guid GameId,
    int Kills, int Deaths, int Assists,
    double TotalDamage, int Rounds, double KastPercent);

public record MatchStatsDto(Guid Id, Guid PlayerId, Guid GameId, int Kills, int Deaths,
    int Assists, double TotalDamage, int Rounds, double KastPercent, double Rating, DateTime CreatedAt);

public interface IMatchStatsService
{
    Task<MatchStatsDto> RegisterStatsAsync(RegisterStatsRequest request);
}
