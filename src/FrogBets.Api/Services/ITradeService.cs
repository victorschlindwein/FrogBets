namespace FrogBets.Api.Services;

public record TradeListingDto(
    Guid UserId, string Username,
    Guid TeamId, string TeamName,
    DateTime CreatedAt);

public record TradeOfferDto(
    Guid Id,
    Guid OfferedUserId, string OfferedUsername,
    Guid TargetUserId, string TargetUsername,
    string ProposerTeamName, string ReceiverTeamName,
    string Status, DateTime CreatedAt);

public interface ITradeService
{
    Task<IReadOnlyList<TradeListingDto>> GetListingsAsync();
    Task AddListingAsync(Guid requesterId, Guid targetUserId);
    Task RemoveListingAsync(Guid requesterId, Guid targetUserId);
    Task<IReadOnlyList<TradeOfferDto>> GetReceivedOffersAsync(Guid requesterId);
    Task<Guid> CreateOfferAsync(Guid requesterId, Guid offeredUserId, Guid targetUserId);
    Task AcceptOfferAsync(Guid requesterId, Guid offerId);
    Task RejectOfferAsync(Guid requesterId, Guid offerId);
    Task DirectSwapAsync(Guid userAId, Guid userBId);
}
