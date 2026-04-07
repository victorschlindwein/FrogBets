using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace FrogBets.Api.Services;

public class InviteService : IInviteService
{
    private readonly FrogBetsDbContext _db;

    public InviteService(FrogBetsDbContext db)
    {
        _db = db;
    }

    public async Task<InviteResult> GenerateAsync(DateTime expiresAt, string? description)
    {
        var invite = new Invite
        {
            Id = Guid.NewGuid(),
            Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLower(),
            Description = description,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        _db.Invites.Add(invite);
        await _db.SaveChangesAsync();

        return ToResult(invite);
    }

    public async Task<IEnumerable<InviteResult>> GetAllAsync()
    {
        var invites = await _db.Invites.AsNoTracking().ToListAsync();
        return invites.Select(ToResult);
    }

    public async Task RevokeAsync(Guid id)
    {
        var invite = await _db.Invites.FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new KeyNotFoundException($"Invite {id} not found.");

        var status = CalculateStatus(invite);

        if (status == InviteStatus.Used)
            throw new InvalidOperationException("INVITE_ALREADY_USED");

        if (status == InviteStatus.Expired)
            throw new InvalidOperationException("INVITE_ALREADY_EXPIRED");

        invite.ExpiresAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<Invite> ValidateAsync(string token)
    {
        var invite = await _db.Invites.FirstOrDefaultAsync(i => i.Token == token);

        if (invite is null)
            throw new InvalidOperationException("INVALID_INVITE");

        if (invite.UsedAt is not null)
            throw new InvalidOperationException("INVITE_ALREADY_USED");

        if (invite.ExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("INVITE_EXPIRED");

        return invite;
    }

    public async Task MarkUsedAsync(Guid inviteId, Guid userId)
    {
        var invite = await _db.Invites.FirstOrDefaultAsync(i => i.Id == inviteId)
            ?? throw new KeyNotFoundException($"Invite {inviteId} not found.");

        invite.UsedAt = DateTime.UtcNow;
        invite.UsedByUserId = userId;

        await _db.SaveChangesAsync();
    }

    private static InviteStatus CalculateStatus(Invite invite)
    {
        if (invite.UsedAt is not null)
            return InviteStatus.Used;

        if (invite.ExpiresAt <= DateTime.UtcNow)
            return InviteStatus.Expired;

        return InviteStatus.Pending;
    }

    private static InviteResult ToResult(Invite invite) =>
        new(invite.Id, invite.Token, invite.Description, invite.ExpiresAt, invite.CreatedAt, CalculateStatus(invite));
}
