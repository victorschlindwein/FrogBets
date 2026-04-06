namespace FrogBets.Domain.Entities;

public class TradeListing
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid TeamId { get; set; }
    public CS2Team Team { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
