using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Services;

public class BetService : IBetService
{
    private readonly FrogBetsDbContext _db;
    private readonly IBalanceService _balanceService;

    public BetService(FrogBetsDbContext db, IBalanceService balanceService)
    {
        _db = db;
        _balanceService = balanceService;
    }

    /// <inheritdoc/>
    public async Task<Guid> CreateBetAsync(Guid creatorId, Guid marketId, string creatorOption, decimal amount)
    {
        // Load market with its game
        var market = await _db.Markets
            .Include(m => m.Game)
            .FirstOrDefaultAsync(m => m.Id == marketId)
            ?? throw new KeyNotFoundException($"Market {marketId} not found.");

        // Validate market is open
        if (market.Status != MarketStatus.Open)
            throw new InvalidOperationException("MARKET_NOT_OPEN");

        // Validate game is still scheduled (not started)
        if (market.Game.Status != GameStatus.Scheduled)
            throw new InvalidOperationException("GAME_ALREADY_STARTED");

        // Validate no duplicate bet by same user on same market
        var hasDuplicate = await _db.Bets.AnyAsync(b =>
            b.MarketId == marketId &&
            b.CreatorId == creatorId &&
            (b.Status == BetStatus.Pending || b.Status == BetStatus.Active));

        if (hasDuplicate)
            throw new InvalidOperationException("DUPLICATE_BET_ON_MARKET");

        // Reserve balance — throws INSUFFICIENT_BALANCE if not enough
        await _balanceService.ReserveBalanceAsync(creatorId, amount);

        // Create the bet
        var bet = new Bet
        {
            Id            = Guid.NewGuid(),
            MarketId      = marketId,
            CreatorId     = creatorId,
            CreatorOption = creatorOption,
            Amount        = amount,
            Status        = BetStatus.Pending,
            CreatedAt     = DateTime.UtcNow,
        };

        _db.Bets.Add(bet);
        await _db.SaveChangesAsync();

        return bet.Id;
    }

    /// <inheritdoc/>
    public async Task CoverBetAsync(Guid coverId, Guid betId)
    {
        // Use a Serializable transaction to guarantee exclusive coverage (SELECT FOR UPDATE semantics)
        await using var tx = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        // Load bet with its market (needed for opposite-option logic)
        var bet = await _db.Bets
            .Include(b => b.Market)
            .FirstOrDefaultAsync(b => b.Id == betId)
            ?? throw new KeyNotFoundException($"Bet {betId} not found.");

        // Reject self-coverage
        if (bet.CreatorId == coverId)
            throw new InvalidOperationException("CANNOT_COVER_OWN_BET");

        // Reject if bet is not pending
        if (bet.Status != BetStatus.Pending)
            throw new InvalidOperationException("BET_NOT_AVAILABLE");

        // Determine the opposite option for the coverer
        var covererOption = GetOppositeOption(bet.Market.Type, bet.CreatorOption);

        // Reserve coverer's balance — throws INSUFFICIENT_BALANCE if not enough
        await _balanceService.ReserveBalanceAsync(coverId, bet.Amount);

        // Update bet to Active
        bet.CoveredById  = coverId;
        bet.CovererOption = covererOption;
        bet.Status       = BetStatus.Active;
        bet.CoveredAt    = DateTime.UtcNow;

        // Create notification for the bet creator
        _db.Notifications.Add(new Notification
        {
            Id        = Guid.NewGuid(),
            UserId    = bet.CreatorId,
            Message   = "Sua aposta foi coberta!",
            IsRead    = false,
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <inheritdoc/>
    public async Task CancelBetAsync(Guid requesterId, Guid betId)
    {
        var bet = await _db.Bets.FindAsync(betId)
            ?? throw new KeyNotFoundException($"Bet {betId} not found.");

        // Only the creator can cancel
        if (bet.CreatorId != requesterId)
            throw new InvalidOperationException("NOT_BET_OWNER");

        // Only pending bets can be cancelled
        if (bet.Status != BetStatus.Pending)
            throw new InvalidOperationException("CANNOT_CANCEL_ACTIVE_BET");

        // Release the reserved balance back to the creator
        await _balanceService.ReleaseBalanceAsync(requesterId, bet.Amount);

        bet.Status = BetStatus.Cancelled;
        await _db.SaveChangesAsync();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static string GetOppositeOption(MarketType marketType, string creatorOption)
    {
        if (marketType is MarketType.MapWinner or MarketType.SeriesWinner)
        {
            return creatorOption == "TeamA" ? "TeamB" : "TeamA";
        }
        // For player-based markets, prefix with NOT_
        return creatorOption.StartsWith("NOT_")
            ? creatorOption[4..]
            : "NOT_" + creatorOption;
    }

    /// <inheritdoc/>
    public async Task<List<BetDto>> GetUserBetsAsync(Guid userId)
    {
        var bets = await _db.Bets
            .Include(b => b.Market)
            .Where(b => b.CreatorId == userId || b.CoveredById == userId)
            .ToListAsync();

        // Settled bets ordered by SettledAt descending; others by CreatedAt descending
        return bets
            .OrderByDescending(b => b.Status == BetStatus.Settled ? 1 : 0)
            .ThenByDescending(b => b.SettledAt ?? DateTime.MinValue)
            .ThenByDescending(b => b.CreatedAt)
            .Select(ToDto)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<List<BetDto>> GetMarketplaceBetsAsync(Guid excludeUserId)
    {
        var bets = await _db.Bets
            .Include(b => b.Market)
            .Where(b => b.Status == BetStatus.Pending && b.CreatorId != excludeUserId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return bets.Select(ToDto).ToList();
    }

    private static BetDto ToDto(Bet b) => new(
        b.Id,
        b.MarketId,
        b.Market.Type,
        b.Market.MapNumber,
        b.Market.GameId,
        b.CreatorOption,
        b.CovererOption,
        b.Amount,
        b.Status,
        b.Result,
        b.CoveredById,
        b.CreatedAt,
        b.CoveredAt,
        b.SettledAt
    );
}
