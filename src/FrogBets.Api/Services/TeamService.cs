using FrogBets.Domain.Entities;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Services;

public class TeamService : ITeamService
{
    private readonly FrogBetsDbContext _db;

    public TeamService(FrogBetsDbContext db)
    {
        _db = db;
    }

    public async Task<CS2TeamDto> CreateTeamAsync(CreateTeamRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("INVALID_TEAM_NAME");

        var exists = await _db.CS2Teams.AnyAsync(t => t.Name == request.Name);
        if (exists)
            throw new InvalidOperationException("TEAM_NAME_ALREADY_EXISTS");

        var team = new CS2Team
        {
            Id        = Guid.NewGuid(),
            Name      = request.Name,
            LogoUrl   = request.LogoUrl,
            CreatedAt = DateTime.UtcNow,
        };

        _db.CS2Teams.Add(team);
        await _db.SaveChangesAsync();

        return ToDto(team);
    }

    public async Task<IReadOnlyList<CS2TeamDto>> GetTeamsAsync()
    {
        var teams = await _db.CS2Teams
            .AsNoTracking()
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.Name)
            .ToListAsync();

        return teams.Select(ToDto).ToList();
    }

    public async Task DeleteTeamAsync(Guid teamId)
    {
        var team = await _db.CS2Teams.FirstOrDefaultAsync(t => t.Id == teamId && !t.IsDeleted)
            ?? throw new InvalidOperationException("TEAM_NOT_FOUND");

        team.IsDeleted = true;
        team.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<CS2TeamDto> UpdateLogoAsync(Guid teamId, string? logoUrl)
    {
        var team = await _db.CS2Teams.FirstOrDefaultAsync(t => t.Id == teamId && !t.IsDeleted)
            ?? throw new InvalidOperationException("TEAM_NOT_FOUND");

        team.LogoUrl = logoUrl;
        await _db.SaveChangesAsync();

        return ToDto(team);
    }

    private static CS2TeamDto ToDto(CS2Team t) =>
        new(t.Id, t.Name, t.LogoUrl, t.CreatedAt);
}
