using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Services;

public class TeamMembershipService : ITeamMembershipService
{
    private readonly FrogBetsDbContext _db;

    public TeamMembershipService(FrogBetsDbContext db)
    {
        _db = db;
    }

    public async Task AssignLeaderAsync(Guid teamId, Guid userId)
    {
        var team = await _db.CS2Teams.FirstOrDefaultAsync(t => t.Id == teamId && !t.IsDeleted)
            ?? throw new InvalidOperationException("TEAM_NOT_FOUND");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new InvalidOperationException("USER_NOT_FOUND");

        if (user.TeamId != teamId)
        {
            // Se o usuário pertence a outro time e já é líder lá, rejeitar
            if (user.IsTeamLeader)
                throw new InvalidOperationException("ALREADY_LEADER_OF_OTHER_TEAM");

            throw new InvalidOperationException("USER_NOT_IN_TEAM");
        }

        // Remover líder atual do time, se existir
        var currentLeader = await _db.Users
            .FirstOrDefaultAsync(u => u.IsTeamLeader && u.TeamId == teamId && u.Id != userId);

        if (currentLeader is not null)
        {
            currentLeader.IsTeamLeader = false;
        }

        user.IsTeamLeader = true;
        await _db.SaveChangesAsync();
    }

    public async Task RemoveLeaderAsync(Guid teamId)
    {
        _ = await _db.CS2Teams.FirstOrDefaultAsync(t => t.Id == teamId && !t.IsDeleted)
            ?? throw new InvalidOperationException("TEAM_NOT_FOUND");

        var leader = await _db.Users
            .FirstOrDefaultAsync(u => u.IsTeamLeader && u.TeamId == teamId);

        if (leader is not null)
        {
            leader.IsTeamLeader = false;
            await _db.SaveChangesAsync();
        }
    }

    public async Task MoveUserAsync(Guid requesterId, bool requesterIsAdmin, Guid targetUserId, Guid? destinationTeamId)
    {
        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new InvalidOperationException("USER_NOT_FOUND");

        if (!requesterIsAdmin)
        {
            var requester = await _db.Users.FirstOrDefaultAsync(u => u.Id == requesterId)
                ?? throw new InvalidOperationException("USER_NOT_FOUND");

            if (!requester.IsTeamLeader || requester.TeamId != targetUser.TeamId)
                throw new InvalidOperationException("FORBIDDEN");
        }

        if (destinationTeamId.HasValue)
        {
            _ = await _db.CS2Teams.FirstOrDefaultAsync(t => t.Id == destinationTeamId.Value && !t.IsDeleted)
                ?? throw new InvalidOperationException("TEAM_NOT_FOUND");
        }

        // Se o usuário alvo era líder, remover o papel automaticamente
        if (targetUser.IsTeamLeader)
        {
            targetUser.IsTeamLeader = false;
        }

        var previousTeamId = targetUser.TeamId;
        targetUser.TeamId = destinationTeamId;

        // Remover trade listing automaticamente ao mudar de time (Requisito 4.6)
        var listing = await _db.TradeListings.FirstOrDefaultAsync(tl => tl.UserId == targetUserId);
        if (listing is not null)
            _db.TradeListings.Remove(listing);

        // Sync CS2Player: create if moving to a team and no player exists yet;
        // clear TeamId if removing from team (preserves stats history)
        var existingPlayer = await _db.CS2Players.FirstOrDefaultAsync(p => p.UserId == targetUserId);

        if (destinationTeamId.HasValue)
        {
            if (existingPlayer is null)
            {
                _db.CS2Players.Add(new FrogBets.Domain.Entities.CS2Player
                {
                    Id           = Guid.NewGuid(),
                    UserId       = targetUserId,
                    Nickname     = targetUser.Username,
                    TeamId       = destinationTeamId.Value,
                    PlayerScore  = 0.0,
                    MatchesCount = 0,
                    CreatedAt    = DateTime.UtcNow,
                });
            }
            else
            {
                existingPlayer.TeamId = destinationTeamId.Value;
            }
        }
        else if (existingPlayer is not null)
        {
            // Removing from team: clear TeamId, preserving stats history
            existingPlayer.TeamId = null;
        }

        await _db.SaveChangesAsync();
    }
}
