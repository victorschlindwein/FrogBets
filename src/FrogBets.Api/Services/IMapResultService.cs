namespace FrogBets.Api.Services;

public record CreateMapResultRequest(Guid GameId, int MapNumber, int Rounds);
public record MapResultDto(Guid Id, Guid GameId, int MapNumber, int Rounds, DateTime CreatedAt);

public interface IMapResultService
{
    Task<MapResultDto> CreateMapResultAsync(CreateMapResultRequest request);
    Task<IReadOnlyList<MapResultDto>> GetByGameAsync(Guid gameId);
}
