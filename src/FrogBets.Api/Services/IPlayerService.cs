namespace FrogBets.Api.Services;

public record CS2PlayerDto(Guid Id, string Nickname, string? RealName, Guid? TeamId, string? TeamName,
    string? PhotoUrl, double PlayerScore, int MatchesCount, DateTime CreatedAt, string? Username);
public record PlayerRankingItemDto(int Position, Guid PlayerId, string Nickname, string TeamName,
    double PlayerScore, int MatchesCount);
public interface IPlayerService
{
    Task<IReadOnlyList<CS2PlayerDto>> GetPlayersAsync();
    Task<IReadOnlyList<CS2PlayerDto>> GetPlayersByTeamAsync(Guid teamId);
    Task<IReadOnlyList<PlayerRankingItemDto>> GetRankingAsync();
    Task<CS2PlayerDto> CreatePlayerAsync(Guid userId, Guid teamId);
    Task<CS2PlayerDto> AssignTeamAsync(Guid playerId, Guid teamId);
}
