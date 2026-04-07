using FrogBets.Api.Services;
using FrogBets.Domain.Entities;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Tests;

public class TeamServiceTests
{
    private static FrogBetsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FrogBetsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FrogBetsDbContext(options);
    }

    // ── CreateTeamAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTeam_ValidName_ReturnsDto()
    {
        await using var db = CreateDb();
        var svc = new TeamService(db);

        var result = await svc.CreateTeamAsync(new CreateTeamRequest("FrogTeam", null));

        Assert.Equal("FrogTeam", result.Name);
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task CreateTeam_EmptyName_ThrowsInvalidTeamName()
    {
        await using var db = CreateDb();
        var svc = new TeamService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateTeamAsync(new CreateTeamRequest("", null)));

        Assert.Equal("INVALID_TEAM_NAME", ex.Message);
    }

    [Fact]
    public async Task CreateTeam_DuplicateName_ThrowsTeamNameAlreadyExists()
    {
        await using var db = CreateDb();
        var svc = new TeamService(db);

        await svc.CreateTeamAsync(new CreateTeamRequest("DupTeam", null));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateTeamAsync(new CreateTeamRequest("DupTeam", null)));

        Assert.Equal("TEAM_NAME_ALREADY_EXISTS", ex.Message);
    }

    // ── GetTeamsAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTeams_ReturnsAllTeamsOrderedByName()
    {
        await using var db = CreateDb();
        db.CS2Teams.AddRange(
            new CS2Team { Id = Guid.NewGuid(), Name = "Zebra", CreatedAt = DateTime.UtcNow },
            new CS2Team { Id = Guid.NewGuid(), Name = "Alpha", CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
        var svc = new TeamService(db);

        var teams = await svc.GetTeamsAsync();

        Assert.Equal("Alpha", teams[0].Name);
        Assert.Equal("Zebra", teams[1].Name);
    }

    [Fact]
    public async Task GetTeams_EmptyDb_ReturnsEmptyList()
    {
        await using var db = CreateDb();
        var svc = new TeamService(db);

        var teams = await svc.GetTeamsAsync();

        Assert.Empty(teams);
    }
}
