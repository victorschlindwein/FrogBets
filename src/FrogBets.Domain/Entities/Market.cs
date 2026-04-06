using FrogBets.Domain.Enums;

namespace FrogBets.Domain.Entities;

public class Market
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public MarketType Type { get; set; }
    public int? MapNumber { get; set; }
    public MarketStatus Status { get; set; }
    public string? WinningOption { get; set; }

    // Navigation
    public Game Game { get; set; } = null!;
    public ICollection<Bet> Bets { get; set; } = new List<Bet>();
    public ICollection<GameResult> GameResults { get; set; } = new List<GameResult>();
}
