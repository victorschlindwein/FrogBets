namespace FrogBets.Domain.Entities;

public class CS2Player
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string? RealName { get; set; }
    public string? PhotoUrl { get; set; }
    public double PlayerScore { get; set; } = 0.0;
    public int MatchesCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; }

    // Navigation
    public CS2Team Team { get; set; } = null!;
    public ICollection<MatchStats> Stats { get; set; } = new List<MatchStats>();
}
