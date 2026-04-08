namespace FrogBets.Api.Services;

public record CS2PlayerDto(Guid Id, string Nickname, string? RealName, Guid TeamId, string TeamName,
    string? PhotoUrl, double PlayerScore, int MatchesCount, DateTime CreatedAt, string? Username);
public record PlayerRankingItemDto(int Position, Guid PlayerId, string Nickname, string TeamName,
    double PlayerScore, int MatchesCount);
public interface IPlayerService
{
    Task<IReadOnlyList<CS2PlayerDto>> GetPlayersAsync();
    Task<IReadOnlyList<PlayerRankingItemDto>> GetRankingAsync();
}
