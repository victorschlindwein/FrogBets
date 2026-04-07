using FrogBets.Domain.Enums;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Services;

public class SettlementService : ISettlementService
{
    private readonly FrogBetsDbContext _db;
    private readonly IBalanceService _balanceService;

    public SettlementService(FrogBetsDbContext db, IBalanceService balanceService)
    {
        _db = db;
        _balanceService = balanceService;
    }

    /// <inheritdoc/>
    public async Task SettleMarketAsync(Guid marketId, string winningOption, bool isVoided = false)
    {
        // Use a single Serializable transaction to ensure atomic settlement of all bets
        await using var tx = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        var market = await _db.Markets
            .Include(m => m.Game)
            .ThenInclude(g => g.Markets)
            .FirstOrDefaultAsync(m => m.Id == marketId)
            ?? throw new KeyNotFoundException($"Market {marketId} not found.");

        var activeBets = await _db.Bets
            .Where(b => b.MarketId == marketId && b.Status == BetStatus.Active)
            .ToListAsync();

        var now = DateTime.UtcNow;

        // Load all affected users once
        var affectedUserIds = activeBets
            .SelectMany(b => new[] { b.CreatorId, b.CoveredById!.Value })
            .Distinct()
            .ToList();
        var users = await _db.Users
            .Where(u => affectedUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        foreach (var bet in activeBets)
        {
            if (isVoided)
            {
                // Return stakes to both sides (inline balance mutations)
                var creator = users[bet.CreatorId];
                creator.ReservedBalance -= bet.Amount;
                creator.VirtualBalance += bet.Amount;

                var coverer = users[bet.CoveredById!.Value];
                coverer.ReservedBalance -= bet.Amount;
                coverer.VirtualBalance += bet.Amount;

                bet.Status = BetStatus.Voided;
                bet.Result = BetResult.Voided;
                bet.SettledAt = now;
            }
            else
            {
                bool creatorWins = bet.CreatorOption == winningOption;
                Guid winnerId = creatorWins ? bet.CreatorId : bet.CoveredById!.Value;
                Guid loserId = creatorWins ? bet.CoveredById!.Value : bet.CreatorId;

                // Credit winner: VirtualBalance += 2*amount, ReservedBalance -= amount (inline)
                var winner = users[winnerId];
                winner.VirtualBalance += 2 * bet.Amount;
                winner.ReservedBalance -= bet.Amount;

                // Deduct loser's reserved stake (inline)
                var loser = users[loserId];
                if (loser.ReservedBalance < bet.Amount)
                    throw new InvalidOperationException("INSUFFICIENT_RESERVED_BALANCE");
                loser.ReservedBalance -= bet.Amount;

                // Update win/loss counters
                winner.WinsCount++;
                loser.LossesCount++;

                bet.Result = creatorWins ? BetResult.CreatorWon : BetResult.CovererWon;
                bet.Status = BetStatus.Settled;
                bet.SettledAt = now;
            }
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>
    /// Deducts <paramref name="amount"/> from the loser's ReservedBalance (stake consumed by winner).
    /// Does NOT return it to VirtualBalance.
    /// </summary>
    private async Task DeductReservedAsync(Guid userId, decimal amount)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (user.ReservedBalance < amount)
            throw new InvalidOperationException("INSUFFICIENT_RESERVED_BALANCE");

        user.ReservedBalance -= amount;
        // SaveChanges is called by the caller after all bets are processed
    }
}
