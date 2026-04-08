namespace FrogBets.Api.Services;

public record CreateTeamRequest(string Name, string? LogoUrl);
public record CS2TeamDto(Guid Id, string Name, string? LogoUrl, DateTime CreatedAt);

public interface ITeamService
{
    Task<CS2TeamDto> CreateTeamAsync(CreateTeamRequest request);
    Task<IReadOnlyList<CS2TeamDto>> GetTeamsAsync();
    Task DeleteTeamAsync(Guid teamId);
    Task<CS2TeamDto> UpdateLogoAsync(Guid teamId, string? logoUrl);
}
