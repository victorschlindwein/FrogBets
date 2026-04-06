using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;

namespace FrogBets.Api.Services;

public record InviteResult(
    Guid Id,
    string Token,
    string? Description,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    InviteStatus Status
);

public interface IInviteService
{
    /// <summary>
    /// Generates a new invite token with the given expiration and optional description.
    /// </summary>
    Task<InviteResult> GenerateAsync(DateTime expiresAt, string? description);

    /// <summary>
    /// Returns all invites with their calculated status.
    /// </summary>
    Task<IEnumerable<InviteResult>> GetAllAsync();

    /// <summary>
    /// Revokes a pending invite by setting ExpiresAt = UtcNow.
    /// Throws InvalidOperationException if already used or expired.
    /// </summary>
    Task RevokeAsync(Guid id);

    /// <summary>
    /// Validates a token and returns the Invite if valid.
    /// Throws InvalidOperationException with codes: INVALID_INVITE, INVITE_ALREADY_USED, INVITE_EXPIRED.
    /// </summary>
    Task<Invite> ValidateAsync(string token);

    /// <summary>
    /// Marks an invite as used by the given user.
    /// </summary>
    Task MarkUsedAsync(Guid inviteId, Guid userId);
}
