namespace FrogBets.Domain.Entities;

public enum TradeOfferStatus { Pending, Accepted, Rejected, Cancelled }

public class TradeOffer
{
    public Guid Id { get; set; }
    public Guid OfferedUserId { get; set; }
    public User OfferedUser { get; set; } = null!;
    public Guid TargetUserId { get; set; }
    public User TargetUser { get; set; } = null!;
    public Guid ProposerTeamId { get; set; }
    public CS2Team ProposerTeam { get; set; } = null!;
    public Guid ReceiverTeamId { get; set; }
    public CS2Team ReceiverTeam { get; set; } = null!;
    public TradeOfferStatus Status { get; set; } = TradeOfferStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
