namespace FrogBets.Api.Services;

public interface ITeamMembershipService
{
    Task AssignLeaderAsync(Guid teamId, Guid userId);
    Task RemoveLeaderAsync(Guid teamId);
    Task MoveUserAsync(Guid requesterId, bool requesterIsAdmin, Guid targetUserId, Guid? destinationTeamId);
}
