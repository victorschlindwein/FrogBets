using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Services;

public class GameService : IGameService
{
    private readonly FrogBetsDbContext _db;
    private readonly ISettlementService _settlementService;

    // Map-level market types (one per map)
    private static readonly MarketType[] MapMarketTypes =
    [
        MarketType.MapWinner,
        MarketType.TopKills,
        MarketType.MostDeaths,
        MarketType.MostUtilityDamage,
    ];

    public GameService(FrogBetsDbContext db, ISettlementService settlementService)
    {
        _db = db;
        _settlementService = settlementService;
    }

    /// <inheritdoc/>
    public async Task<Guid> CreateGameAsync(CreateGameRequest request)
    {
        var game = new Game
        {
            Id           = Guid.NewGuid(),
            TeamA        = request.TeamA,
            TeamB        = request.TeamB,
            ScheduledAt  = request.ScheduledAt,
            NumberOfMaps = request.NumberOfMaps,
            Status       = GameStatus.Scheduled,
            CreatedAt    = DateTime.UtcNow,
        };

        // Auto-create markets: one per map-level type per map
        for (var map = 1; map <= request.NumberOfMaps; map++)
        {
            foreach (var type in MapMarketTypes)
            {
                game.Markets.Add(new Market
                {
                    Id        = Guid.NewGuid(),
                    GameId    = game.Id,
                    Type      = type,
                    MapNumber = map,
                    Status    = MarketStatus.Open,
                });
            }
        }

        // Series winner market (MapNumber = null)
        game.Markets.Add(new Market
        {
            Id        = Guid.NewGuid(),
            GameId    = game.Id,
            Type      = MarketType.SeriesWinner,
            MapNumber = null,
            Status    = MarketStatus.Open,
        });

        _db.Games.Add(game);
        await _db.SaveChangesAsync();

        return game.Id;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GameDto>> GetGamesAsync()
    {
        var games = await _db.Games
            .AsNoTracking()
            .Include(g => g.Markets)
            .OrderBy(g => g.ScheduledAt)
            .ToListAsync();

        return games.Select(ToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task StartGameAsync(Guid gameId)
    {
        var game = await _db.Games
            .Include(g => g.Markets)
            .FirstOrDefaultAsync(g => g.Id == gameId)
            ?? throw new KeyNotFoundException($"Game {gameId} not found.");

        game.Status = GameStatus.InProgress;

        foreach (var market in game.Markets.Where(m => m.Status == MarketStatus.Open))
            market.Status = MarketStatus.Closed;

        await _db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task RegisterResultAsync(Guid gameId, RegisterResultRequest request, Guid adminId)
    {
        var game = await _db.Games
            .Include(g => g.Markets)
            .FirstOrDefaultAsync(g => g.Id == gameId)
            ?? throw new KeyNotFoundException($"Game {gameId} not found.");

        if (game.Status == GameStatus.Finished)
            throw new InvalidOperationException("GAME_ALREADY_FINISHED");

        var market = game.Markets.FirstOrDefault(m => m.Id == request.MarketId)
            ?? throw new KeyNotFoundException($"Market {request.MarketId} not found in game {gameId}.");

        // Record the result
        var result = new GameResult
        {
            Id                  = Guid.NewGuid(),
            GameId              = gameId,
            MarketId            = request.MarketId,
            WinningOption       = request.WinningOption,
            MapNumber           = request.MapNumber,
            RegisteredAt        = DateTime.UtcNow,
            RegisteredByAdminId = adminId,
        };

        _db.GameResults.Add(result);

        // Settle the market
        market.WinningOption = request.WinningOption;
        market.Status        = MarketStatus.Settled;

        // Check if all markets are Settled or Voided → finish the game
        if (game.Markets.All(m => m.Status is MarketStatus.Settled or MarketStatus.Voided))
            game.Status = GameStatus.Finished;

        await _db.SaveChangesAsync();

        // Settle all active bets for this market
        await _settlementService.SettleMarketAsync(request.MarketId, request.WinningOption, isVoided: false);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static GameDto ToDto(Game g) => new(
        g.Id,
        g.TeamA,
        g.TeamB,
        g.ScheduledAt,
        g.NumberOfMaps,
        g.Status.ToString(),
        g.CreatedAt,
        g.Markets.Select(m => new MarketDto(
            m.Id,
            m.Type.ToString(),
            m.MapNumber,
            m.Status.ToString(),
            m.WinningOption)).ToList());
}
