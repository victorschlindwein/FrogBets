namespace FrogBets.Domain.Entities;

public class MatchStats
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public Guid MapResultId { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public double TotalDamage { get; set; }
    public double KastPercent { get; set; }
    public double Rating { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public CS2Player Player { get; set; } = null!;
    public MapResult MapResult { get; set; } = null!;
}
