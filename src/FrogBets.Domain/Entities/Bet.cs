using FrogBets.Domain.Enums;

namespace FrogBets.Domain.Entities;

public class Bet
{
    public Guid Id { get; set; }
    public Guid MarketId { get; set; }
    public Guid CreatorId { get; set; }
    public Guid? CoveredById { get; set; }
    public string CreatorOption { get; set; } = string.Empty;
    public string? CovererOption { get; set; }
    public decimal Amount { get; set; }
    public BetStatus Status { get; set; }
    public BetResult? Result { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CoveredAt { get; set; }
    public DateTime? SettledAt { get; set; }

    // Navigation
    public Market Market { get; set; } = null!;
    public User Creator { get; set; } = null!;
    public User? CoveredBy { get; set; }
}
