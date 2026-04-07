using FrogBets.Api.Services;
using FrogBets.Domain.Entities;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FrogBets.Tests;

public class InviteServiceTests
{
    private static FrogBetsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FrogBetsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FrogBetsDbContext(options);
    }

    private static InviteService CreateService(FrogBetsDbContext db) => new(db);

    // ── GenerateAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate_CreatesInviteWithUniqueToken()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var result = await svc.GenerateAsync(DateTime.UtcNow.AddDays(7), "Test");

        Assert.NotEmpty(result.Token);
        Assert.Equal(32, result.Token.Length); // 16 bytes hex = 32 chars
    }

    [Fact]
    public async Task Generate_TwoInvites_HaveDifferentTokens()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var r1 = await svc.GenerateAsync(DateTime.UtcNow.AddDays(1), null);
        var r2 = await svc.GenerateAsync(DateTime.UtcNow.AddDays(1), null);

        Assert.NotEqual(r1.Token, r2.Token);
    }

    [Fact]
    public async Task Generate_StatusIsPending()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var result = await svc.GenerateAsync(DateTime.UtcNow.AddDays(1), null);

        Assert.Equal(FrogBets.Domain.Enums.InviteStatus.Pending, result.Status);
    }

    // ── ValidateAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_ValidToken_ReturnsInvite()
    {
        await using var db = CreateDb();
        var invite = new Invite
        {
            Id        = Guid.NewGuid(),
            Token     = "validtoken0000000000000000000001",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow,
        };
        db.Invites.Add(invite);
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var result = await svc.ValidateAsync(invite.Token);

        Assert.Equal(invite.Id, result.Id);
    }

    [Fact]
    public async Task Validate_NonExistentToken_ThrowsInvalidInvite()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ValidateAsync("doesnotexist00000000000000000000"));

        Assert.Equal("INVALID_INVITE", ex.Message);
    }

    [Fact]
    public async Task Validate_ExpiredToken_ThrowsInviteExpired()
    {
        await using var db = CreateDb();
        var invite = new Invite
        {
            Id        = Guid.NewGuid(),
            Token     = "expiredtoken000000000000000000001",
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-2),
        };
        db.Invites.Add(invite);
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ValidateAsync(invite.Token));

        Assert.Equal("INVITE_EXPIRED", ex.Message);
    }

    [Fact]
    public async Task Validate_UsedToken_ThrowsInviteAlreadyUsed()
    {
        await using var db = CreateDb();
        var invite = new Invite
        {
            Id          = Guid.NewGuid(),
            Token       = "usedtoken0000000000000000000001",
            ExpiresAt   = DateTime.UtcNow.AddDays(1),
            CreatedAt   = DateTime.UtcNow,
            UsedAt      = DateTime.UtcNow,
            UsedByUserId = Guid.NewGuid(),
        };
        db.Invites.Add(invite);
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ValidateAsync(invite.Token));

        Assert.Equal("INVITE_ALREADY_USED", ex.Message);
    }

    // ── RevokeAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Revoke_PendingInvite_SetsExpiresAtToNow()
    {
        await using var db = CreateDb();
        var invite = new Invite
        {
            Id        = Guid.NewGuid(),
            Token     = "revoketoken000000000000000000001",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
        };
        db.Invites.Add(invite);
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        await svc.RevokeAsync(invite.Id);

        var updated = await db.Invites.FindAsync(invite.Id);
        Assert.True(updated!.ExpiresAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task Revoke_UsedInvite_ThrowsInviteAlreadyUsed()
    {
        await using var db = CreateDb();
        var invite = new Invite
        {
            Id           = Guid.NewGuid(),
            Token        = "usedrevoke000000000000000000001",
            ExpiresAt    = DateTime.UtcNow.AddDays(1),
            CreatedAt    = DateTime.UtcNow,
            UsedAt       = DateTime.UtcNow,
            UsedByUserId = Guid.NewGuid(),
        };
        db.Invites.Add(invite);
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RevokeAsync(invite.Id));

        Assert.Equal("INVITE_ALREADY_USED", ex.Message);
    }

    [Fact]
    public async Task Revoke_NonExistent_ThrowsKeyNotFoundException()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.RevokeAsync(Guid.NewGuid()));
    }

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsAllInvitesWithCorrectStatus()
    {
        await using var db = CreateDb();
        db.Invites.AddRange(
            new Invite { Id = Guid.NewGuid(), Token = "tok1000000000000000000000000001", ExpiresAt = DateTime.UtcNow.AddDays(1), CreatedAt = DateTime.UtcNow },
            new Invite { Id = Guid.NewGuid(), Token = "tok2000000000000000000000000001", ExpiresAt = DateTime.UtcNow.AddDays(-1), CreatedAt = DateTime.UtcNow.AddDays(-2) }
        );
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var results = (await svc.GetAllAsync()).ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Status == FrogBets.Domain.Enums.InviteStatus.Pending);
        Assert.Contains(results, r => r.Status == FrogBets.Domain.Enums.InviteStatus.Expired);
    }
}
