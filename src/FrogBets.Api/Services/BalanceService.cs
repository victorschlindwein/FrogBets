using System.Data;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Services;

public class BalanceService : IBalanceService
{
    private readonly FrogBetsDbContext _db;

    public BalanceService(FrogBetsDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task ReserveBalanceAsync(Guid userId, decimal amount)
    {
        var ownTx = _db.Database.CurrentTransaction is null;
        await using var tx = ownTx
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (user.VirtualBalance < amount)
            throw new InvalidOperationException("INSUFFICIENT_BALANCE");

        user.VirtualBalance -= amount;
        user.ReservedBalance += amount;

        await _db.SaveChangesAsync();
        if (ownTx) await tx!.CommitAsync();
    }

    /// <inheritdoc/>
    public async Task ReleaseBalanceAsync(Guid userId, decimal amount)
    {
        var ownTx = _db.Database.CurrentTransaction is null;
        await using var tx = ownTx
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        user.ReservedBalance -= amount;
        user.VirtualBalance += amount;

        await _db.SaveChangesAsync();
        if (ownTx) await tx!.CommitAsync();
    }

    /// <inheritdoc/>
    public async Task CreditWinnerAsync(Guid winnerId, decimal amount)
    {
        var ownTx = _db.Database.CurrentTransaction is null;
        await using var tx = ownTx
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == winnerId)
            ?? throw new KeyNotFoundException($"User {winnerId} not found.");

        user.VirtualBalance += 2 * amount;
        user.ReservedBalance -= amount;

        await _db.SaveChangesAsync();
        if (ownTx) await tx!.CommitAsync();
    }
}
