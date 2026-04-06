namespace FrogBets.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public decimal VirtualBalance { get; set; }
    public decimal ReservedBalance { get; set; }
    public int WinsCount { get; set; }
    public int LossesCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? TeamId { get; set; }
    public bool IsTeamLeader { get; set; } = false;

    // Navigation
    public ICollection<Bet> CreatedBets { get; set; } = new List<Bet>();
    public ICollection<Bet> CoveredBets { get; set; } = new List<Bet>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public CS2Team? Team { get; set; }
}
