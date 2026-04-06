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
        var market = await _db.Markets
            .Include(m => m.Game)
            .ThenInclude(g => g.Markets)
            .FirstOrDefaultAsync(m => m.Id == marketId)
            ?? throw new KeyNotFoundException($"Market {marketId} not found.");

        var activeBets = await _db.Bets
            .Where(b => b.MarketId == marketId && b.Status == BetStatus.Active)
            .ToListAsync();

        var now = DateTime.UtcNow;

        foreach (var bet in activeBets)
        {
            if (isVoided)
            {
                // Return stakes to both sides
                await _balanceService.ReleaseBalanceAsync(bet.CreatorId, bet.Amount);
                await _balanceService.ReleaseBalanceAsync(bet.CoveredById!.Value, bet.Amount);

                bet.Status = BetStatus.Voided;
                bet.Result = BetResult.Voided;
                bet.SettledAt = now;
            }
            else
            {
                bool creatorWins = bet.CreatorOption == winningOption;
                Guid winnerId = creatorWins ? bet.CreatorId : bet.CoveredById!.Value;
                Guid loserId = creatorWins ? bet.CoveredById!.Value : bet.CreatorId;

                // Credit winner: VirtualBalance += 2*amount, ReservedBalance -= amount
                await _balanceService.CreditWinnerAsync(winnerId, bet.Amount);
                // Deduct loser's reserved stake (consumed by winner's credit)
                await DeductReservedAsync(loserId, bet.Amount);

                // Update win/loss counters
                var winner = await _db.Users.FindAsync(winnerId);
                var loser = await _db.Users.FindAsync(loserId);
                if (winner != null) winner.WinsCount++;
                if (loser != null) loser.LossesCount++;

                bet.Result = creatorWins ? BetResult.CreatorWon : BetResult.CovererWon;
                bet.Status = BetStatus.Settled;
                bet.SettledAt = now;
            }
        }

        await _db.SaveChangesAsync();

        // Check if all markets for the game are Settled or Voided → finish the game
        var game = market.Game;
        if (game.Markets.All(m => m.Status is MarketStatus.Settled or MarketStatus.Voided))
        {
            game.Status = GameStatus.Finished;
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Deducts <paramref name="amount"/> from the loser's ReservedBalance (stake consumed by winner).
    /// Does NOT return it to VirtualBalance.
    /// </summary>
    private async Task DeductReservedAsync(Guid userId, decimal amount)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");
        user.ReservedBalance -= amount;
        // SaveChanges is called by the caller after all bets are processed
    }
}
