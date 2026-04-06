namespace FrogBets.Domain.Entities;

public class Invite
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public Guid? UsedByUserId { get; set; }

    // Navigation
    public User? UsedByUser { get; set; }
}
