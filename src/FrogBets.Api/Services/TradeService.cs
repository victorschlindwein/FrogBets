using FrogBets.Domain.Entities;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Services;

public class TradeService : ITradeService
{
    private readonly FrogBetsDbContext _db;

    public TradeService(FrogBetsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TradeListingDto>> GetListingsAsync()
    {
        var listings = await _db.TradeListings
            .Include(tl => tl.User)
            .Include(tl => tl.Team)
            .AsNoTracking()
            .ToListAsync();

        return listings.Select(tl => new TradeListingDto(
            tl.UserId, tl.User.Username,
            tl.TeamId, tl.Team.Name,
            tl.CreatedAt)).ToList();
    }

    public async Task AddListingAsync(Guid requesterId, Guid targetUserId)
    {
        var requester = await _db.Users.FirstOrDefaultAsync(u => u.Id == requesterId)
            ?? throw new KeyNotFoundException("USER_NOT_FOUND");

        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new KeyNotFoundException("USER_NOT_FOUND");

        if (!requester.IsTeamLeader || requester.TeamId != targetUser.TeamId)
            throw new InvalidOperationException("FORBIDDEN");

        var alreadyListed = await _db.TradeListings.AnyAsync(tl => tl.UserId == targetUserId);
        if (alreadyListed)
            throw new InvalidOperationException("ALREADY_LISTED");

        _db.TradeListings.Add(new TradeListing
        {
            Id = Guid.NewGuid(),
            UserId = targetUserId,
            TeamId = targetUser.TeamId!.Value,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async Task RemoveListingAsync(Guid requesterId, Guid targetUserId)
    {
        var requester = await _db.Users.FirstOrDefaultAsync(u => u.Id == requesterId)
            ?? throw new KeyNotFoundException("USER_NOT_FOUND");

        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new KeyNotFoundException("USER_NOT_FOUND");

        if (!requester.IsTeamLeader || requester.TeamId != targetUser.TeamId)
            throw new InvalidOperationException("FORBIDDEN");

        var listing = await _db.TradeListings.FirstOrDefaultAsync(tl => tl.UserId == targetUserId)
            ?? throw new KeyNotFoundException("USER_NOT_FOUND");

        _db.TradeListings.Remove(listing);
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<TradeOfferDto>> GetReceivedOffersAsync(Guid requesterId)
    {
        var requester = await _db.Users.FirstOrDefaultAsync(u => u.Id == requesterId)
            ?? throw new KeyNotFoundException("USER_NOT_FOUND");

        var offers = await _db.TradeOffers
            .Include(o => o.OfferedUser)
            .Include(o => o.TargetUser)
            .Include(o => o.ProposerTeam)
            .Include(o => o.ReceiverTeam)
            .Where(o => o.ReceiverTeamId == requester.TeamId && o.Status == TradeOfferStatus.Pending)
            .AsNoTracking()
            .ToListAsync();

        return offers.Select(o => new TradeOfferDto(
            o.Id,
            o.OfferedUserId, o.OfferedUser.Username,
            o.TargetUserId, o.TargetUser.Username,
            o.ProposerTeam.Name, o.ReceiverTeam.Name,
            o.Status.ToString(), o.CreatedAt)).ToList();
    }

    public async Task<Guid> CreateOfferAsync(Guid requesterId, Guid offeredUserId, Guid targetUserId)
    {
        var requester = await _db.Users.FirstOrDefaultAsync(u => u.Id == requesterId)
            ?? throw new KeyNotFoundException("USER_NOT_FOUND");

        if (!requester.IsTeamLeader)
            throw new InvalidOperationException("FORBIDDEN");

        var offeredUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == offeredUserId)
            ?? throw new KeyNotFoundException("USER_NOT_FOUND");

        if (offeredUser.TeamId != requester.TeamId)
            throw new InvalidOperationException("FORBIDDEN");

        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new KeyNotFoundException("USER_NOT_FOUND");

        var targetListed = await _db.TradeListings.AnyAsync(tl => tl.UserId == targetUserId);
        if (!targetListed)
            throw new InvalidOperationException("TARGET_NOT_AVAILABLE");

        if (offeredUser.TeamId == targetUser.TeamId)
            throw new InvalidOperationException("SAME_TEAM_TRADE");

        var offer = new TradeOffer
        {
            Id = Guid.NewGuid(),
            OfferedUserId = offeredUserId,
            TargetUserId = targetUserId,
            ProposerTeamId = requester.TeamId!.Value,
            ReceiverTeamId = targetUser.TeamId!.Value,
            Status = TradeOfferStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.TradeOffers.Add(offer);
        await _db.SaveChangesAsync();

        return offer.Id;
    }

    public async Task AcceptOfferAsync(Guid requesterId, Guid offerId)
    {
        var requester = await _db.Users.FirstOrDefaultAsync(u => u.Id == requesterId)
            ?? throw new KeyNotFoundException("USER_NOT_FOUND");

        var offer = await _db.TradeOffers
            .Include(o => o.OfferedUser)
            .Include(o => o.TargetUser)
            .FirstOrDefaultAsync(o => o.Id == offerId)
            ?? throw new KeyNotFoundException($"Offer {offerId} not found.");

        if (offer.ReceiverTeamId != requester.TeamId)
            throw new InvalidOperationException("FORBIDDEN");

        if (offer.Status != TradeOfferStatus.Pending)
            throw new InvalidOperationException("OFFER_NOT_PENDING");

        // Swap TeamIds
        var offeredUser = offer.OfferedUser;
        var targetUser = offer.TargetUser;

        offeredUser.TeamId = offer.ReceiverTeamId;
        targetUser.TeamId = offer.ProposerTeamId;

        offer.Status = TradeOfferStatus.Accepted;
        offer.UpdatedAt = DateTime.UtcNow;

        // Remove trade listings for both members
        var listings = await _db.TradeListings
            .Where(tl => tl.UserId == offeredUser.Id || tl.UserId == targetUser.Id)
            .ToListAsync();
        _db.TradeListings.RemoveRange(listings);

        // Cancel other pending offers involving either member
        var otherOffers = await _db.TradeOffers
            .Where(o => o.Id != offerId
                && o.Status == TradeOfferStatus.Pending
                && (o.OfferedUserId == offeredUser.Id || o.OfferedUserId == targetUser.Id
                    || o.TargetUserId == offeredUser.Id || o.TargetUserId == targetUser.Id))
            .ToListAsync();

        foreach (var other in otherOffers)
        {
            other.Status = TradeOfferStatus.Cancelled;
            other.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task RejectOfferAsync(Guid requesterId, Guid offerId)
    {
        var requester = await _db.Users.FirstOrDefaultAsync(u => u.Id == requesterId)
            ?? throw new KeyNotFoundException("USER_NOT_FOUND");

        var offer = await _db.TradeOffers.FirstOrDefaultAsync(o => o.Id == offerId)
            ?? throw new KeyNotFoundException($"Offer {offerId} not found.");

        if (offer.ReceiverTeamId != requester.TeamId)
            throw new InvalidOperationException("FORBIDDEN");

        if (offer.Status != TradeOfferStatus.Pending)
            throw new InvalidOperationException("OFFER_NOT_PENDING");

        offer.Status = TradeOfferStatus.Rejected;
        offer.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task DirectSwapAsync(Guid userAId, Guid userBId)
    {
        var userA = await _db.Users.FirstOrDefaultAsync(u => u.Id == userAId)
            ?? throw new KeyNotFoundException("USER_NOT_FOUND");

        var userB = await _db.Users.FirstOrDefaultAsync(u => u.Id == userBId)
            ?? throw new KeyNotFoundException("USER_NOT_FOUND");

        if (userA.TeamId == userB.TeamId)
            throw new InvalidOperationException("SAME_TEAM_TRADE");

        // Swap TeamIds
        (userA.TeamId, userB.TeamId) = (userB.TeamId, userA.TeamId);

        // Remove trade listings for both
        var listings = await _db.TradeListings
            .Where(tl => tl.UserId == userAId || tl.UserId == userBId)
            .ToListAsync();
        _db.TradeListings.RemoveRange(listings);

        // Cancel pending offers involving either user
        var pendingOffers = await _db.TradeOffers
            .Where(o => o.Status == TradeOfferStatus.Pending
                && (o.OfferedUserId == userAId || o.OfferedUserId == userBId
                    || o.TargetUserId == userAId || o.TargetUserId == userBId))
            .ToListAsync();

        foreach (var offer in pendingOffers)
        {
            offer.Status = TradeOfferStatus.Cancelled;
            offer.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }
}
