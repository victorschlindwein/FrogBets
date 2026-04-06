using FrogBets.Api.Services;
using FrogBets.Domain.Entities;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FrogBets.Tests;

/// <summary>
/// Unit tests for BalanceService.
/// Uses InMemory database — transactions are no-ops in InMemory, but the logic is fully exercised.
/// </summary>
public class BalanceServiceTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static FrogBetsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FrogBetsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new FrogBetsDbContext(options);
    }

    private static async Task<User> SeedUserAsync(FrogBetsDbContext db,
        decimal virtualBalance = 1000m, decimal reservedBalance = 0m)
    {
        var user = new User
        {
            Id              = Guid.NewGuid(),
            Username        = Guid.NewGuid().ToString("N"),
            PasswordHash    = "hash",
            VirtualBalance  = virtualBalance,
            ReservedBalance = reservedBalance,
            CreatedAt       = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    // ── ReserveBalance ────────────────────────────────────────────────────────

    [Fact]
    public async Task ReserveBalance_SufficientFunds_DecreasesVirtualAndIncreasesReserved()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, virtualBalance: 500m);
        var svc = new BalanceService(db);

        await svc.ReserveBalanceAsync(user.Id, 200m);

        var updated = await db.Users.FindAsync(user.Id);
        Assert.Equal(300m, updated!.VirtualBalance);
        Assert.Equal(200m, updated.ReservedBalance);
    }

    [Fact]
    public async Task ReserveBalance_ExactBalance_Succeeds()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, virtualBalance: 100m);
        var svc = new BalanceService(db);

        await svc.ReserveBalanceAsync(user.Id, 100m);

        var updated = await db.Users.FindAsync(user.Id);
        Assert.Equal(0m, updated!.VirtualBalance);
        Assert.Equal(100m, updated.ReservedBalance);
    }

    [Fact]
    public async Task ReserveBalance_InsufficientFunds_ThrowsWithInsufficientBalanceCode()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, virtualBalance: 50m);
        var svc = new BalanceService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ReserveBalanceAsync(user.Id, 100m));

        Assert.Equal("INSUFFICIENT_BALANCE", ex.Message);
    }

    [Fact]
    public async Task ReserveBalance_InsufficientFunds_DoesNotAlterBalance()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, virtualBalance: 50m, reservedBalance: 10m);
        var svc = new BalanceService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ReserveBalanceAsync(user.Id, 100m));

        var unchanged = await db.Users.FindAsync(user.Id);
        Assert.Equal(50m, unchanged!.VirtualBalance);
        Assert.Equal(10m, unchanged.ReservedBalance);
    }

    [Fact]
    public async Task ReserveBalance_PreservesTotalBalance()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, virtualBalance: 800m, reservedBalance: 200m);
        var svc = new BalanceService(db);
        var totalBefore = user.VirtualBalance + user.ReservedBalance;

        await svc.ReserveBalanceAsync(user.Id, 300m);

        var updated = await db.Users.FindAsync(user.Id);
        Assert.Equal(totalBefore, updated!.VirtualBalance + updated.ReservedBalance);
    }

    // ── ReleaseBalance ────────────────────────────────────────────────────────

    [Fact]
    public async Task ReleaseBalance_IncreasesVirtualAndDecreasesReserved()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, virtualBalance: 300m, reservedBalance: 200m);
        var svc = new BalanceService(db);

        await svc.ReleaseBalanceAsync(user.Id, 200m);

        var updated = await db.Users.FindAsync(user.Id);
        Assert.Equal(500m, updated!.VirtualBalance);
        Assert.Equal(0m, updated.ReservedBalance);
    }

    [Fact]
    public async Task ReleaseBalance_PreservesTotalBalance()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, virtualBalance: 400m, reservedBalance: 600m);
        var svc = new BalanceService(db);
        var totalBefore = user.VirtualBalance + user.ReservedBalance;

        await svc.ReleaseBalanceAsync(user.Id, 150m);

        var updated = await db.Users.FindAsync(user.Id);
        Assert.Equal(totalBefore, updated!.VirtualBalance + updated.ReservedBalance);
    }

    // ── CreditWinner ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreditWinner_CreditsDoubleAmountToVirtualBalance()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, virtualBalance: 0m, reservedBalance: 100m);
        var svc = new BalanceService(db);

        await svc.CreditWinnerAsync(user.Id, 100m);

        var updated = await db.Users.FindAsync(user.Id);
        Assert.Equal(200m, updated!.VirtualBalance);
    }

    [Fact]
    public async Task CreditWinner_ReleasesReservedBalance()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, virtualBalance: 500m, reservedBalance: 100m);
        var svc = new BalanceService(db);

        await svc.CreditWinnerAsync(user.Id, 100m);

        var updated = await db.Users.FindAsync(user.Id);
        Assert.Equal(0m, updated!.ReservedBalance);
    }

    [Fact]
    public async Task CreditWinner_NetGainIsAmountFromOpponent()
    {
        // Winner had 500 virtual + 100 reserved. After winning 100 bet:
        // VirtualBalance = 500 + 200 = 700, ReservedBalance = 100 - 100 = 0
        // Net gain = 100 (the opponent's stake)
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, virtualBalance: 500m, reservedBalance: 100m);
        var svc = new BalanceService(db);

        await svc.CreditWinnerAsync(user.Id, 100m);

        var updated = await db.Users.FindAsync(user.Id);
        Assert.Equal(700m, updated!.VirtualBalance);
        Assert.Equal(0m, updated.ReservedBalance);
    }

    // ── Round-trip: reserve then release ─────────────────────────────────────

    [Fact]
    public async Task ReserveThenRelease_RestoresOriginalBalances()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, virtualBalance: 1000m, reservedBalance: 0m);
        var svc = new BalanceService(db);

        await svc.ReserveBalanceAsync(user.Id, 250m);
        await svc.ReleaseBalanceAsync(user.Id, 250m);

        var updated = await db.Users.FindAsync(user.Id);
        Assert.Equal(1000m, updated!.VirtualBalance);
        Assert.Equal(0m, updated.ReservedBalance);
    }

    // ── User not found ────────────────────────────────────────────────────────

    [Fact]
    public async Task ReserveBalance_UnknownUser_ThrowsKeyNotFoundException()
    {
        await using var db = CreateDb();
        var svc = new BalanceService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.ReserveBalanceAsync(Guid.NewGuid(), 100m));
    }

    [Fact]
    public async Task ReleaseBalance_UnknownUser_ThrowsKeyNotFoundException()
    {
        await using var db = CreateDb();
        var svc = new BalanceService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.ReleaseBalanceAsync(Guid.NewGuid(), 100m));
    }

    [Fact]
    public async Task CreditWinner_UnknownUser_ThrowsKeyNotFoundException()
    {
        await using var db = CreateDb();
        var svc = new BalanceService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.CreditWinnerAsync(Guid.NewGuid(), 100m));
    }
}
