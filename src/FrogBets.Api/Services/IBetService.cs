using FrogBets.Domain.Enums;

namespace FrogBets.Api.Services;

/// <summary>DTO returned by listing endpoints.</summary>
public record BetDto(
    Guid        Id,
    Guid        MarketId,
    MarketType  MarketType,
    int?        MapNumber,
    Guid        GameId,
    string      CreatorOption,
    string?     CovererOption,
    decimal     Amount,
    BetStatus   Status,
    BetResult?  Result,
    Guid?       CoveredById,
    DateTime    CreatedAt,
    DateTime?   CoveredAt,
    DateTime?   SettledAt
);

public interface IBetService
{
    /// <summary>
    /// Creates a new bet for <paramref name="creatorId"/> on <paramref name="marketId"/>.
    /// Validates market is Open, game is Scheduled, no duplicate bet, and sufficient balance.
    /// Throws <see cref="InvalidOperationException"/> with codes:
    ///   "MARKET_NOT_OPEN", "GAME_ALREADY_STARTED", "DUPLICATE_BET_ON_MARKET", "INSUFFICIENT_BALANCE"
    /// </summary>
    Task<Guid> CreateBetAsync(Guid creatorId, Guid marketId, string creatorOption, decimal amount);

    /// <summary>
    /// Covers an existing pending bet on behalf of <paramref name="coverId"/>.
    /// Assigns the opposite option, reserves the coverer's balance, and notifies the creator.
    /// Throws <see cref="KeyNotFoundException"/> if the bet does not exist.
    /// Throws <see cref="InvalidOperationException"/> with codes:
    ///   "CANNOT_COVER_OWN_BET", "BET_NOT_AVAILABLE", "INSUFFICIENT_BALANCE"
    /// </summary>
    Task CoverBetAsync(Guid coverId, Guid betId);

    /// <summary>
    /// Cancels a pending bet created by <paramref name="requesterId"/>.
    /// Releases the reserved balance back to the creator and sets status to Cancelled.
    /// Throws <see cref="KeyNotFoundException"/> if the bet does not exist.
    /// Throws <see cref="InvalidOperationException"/> with codes:
    ///   "NOT_BET_OWNER" if requesterId != bet.CreatorId,
    ///   "CANNOT_CANCEL_ACTIVE_BET" if bet.Status != Pending
    /// </summary>
    Task CancelBetAsync(Guid requesterId, Guid betId);

    /// <summary>
    /// Returns all bets where the user is creator or coverer (Pending, Active, Settled).
    /// Settled bets are ordered by SettledAt descending.
    /// </summary>
    Task<List<BetDto>> GetUserBetsAsync(Guid userId);

    /// <summary>
    /// Returns all Pending bets not created by <paramref name="excludeUserId"/>.
    /// </summary>
    Task<List<BetDto>> GetMarketplaceBetsAsync(Guid excludeUserId);
}
