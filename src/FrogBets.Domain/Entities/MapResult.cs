namespace FrogBets.Domain.Entities;

public class MapResult
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public int MapNumber { get; set; }
    public int Rounds { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Game Game { get; set; } = null!;
    public ICollection<MatchStats> Stats { get; set; } = new List<MatchStats>();
}
