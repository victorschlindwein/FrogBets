using FrogBets.Domain.Enums;

namespace FrogBets.Domain.Entities;

public class Game
{
    public Guid Id { get; set; }
    public string TeamA { get; set; } = string.Empty;
    public string TeamB { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public int NumberOfMaps { get; set; }
    public GameStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public ICollection<Market> Markets { get; set; } = new List<Market>();
}
