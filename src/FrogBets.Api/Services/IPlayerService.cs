namespace FrogBets.Api.Services;

public record CreatePlayerRequest(string Nickname, string? RealName, Guid TeamId, string? PhotoUrl);
public record CS2PlayerDto(Guid Id, string Nickname, string? RealName, Guid TeamId, string TeamName,
    string? PhotoUrl, double PlayerScore, int MatchesCount, DateTime CreatedAt);
public record PlayerRankingItemDto(int Position, Guid PlayerId, string Nickname, string TeamName,
    double PlayerScore, int MatchesCount);

public interface IPlayerService
{
    Task<CS2PlayerDto> CreatePlayerAsync(CreatePlayerRequest request);
    Task<IReadOnlyList<CS2PlayerDto>> GetPlayersAsync();
    Task<IReadOnlyList<PlayerRankingItemDto>> GetRankingAsync();
}
