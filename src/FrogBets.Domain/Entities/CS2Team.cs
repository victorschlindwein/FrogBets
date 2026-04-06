namespace FrogBets.Domain.Entities;

public class CS2Team
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public ICollection<CS2Player> Players { get; set; } = new List<CS2Player>();
}
