namespace FrogBets.Domain.Entities;

public class GameResult
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Guid MarketId { get; set; }
    public string WinningOption { get; set; } = string.Empty;
    public int? MapNumber { get; set; }
    public DateTime RegisteredAt { get; set; }
    public Guid RegisteredByAdminId { get; set; }

    // Navigation
    public Game Game { get; set; } = null!;
    public Market Market { get; set; } = null!;
    public User RegisteredByAdmin { get; set; } = null!;
}
