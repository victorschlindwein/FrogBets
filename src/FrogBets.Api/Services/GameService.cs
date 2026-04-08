using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Services;

public class GameService : IGameService
{
    private readonly FrogBetsDbContext _db;
    private readonly ISettlementService _settlementService;
    private readonly IBalanceService _balanceService;

    // Map-level market types (one per map)
    private static readonly MarketType[] MapMarketTypes =
    [
        MarketType.MapWinner,
        MarketType.TopKills,
        MarketType.MostDeaths,
        MarketType.MostUtilityDamage,
    ];

    public GameService(FrogBetsDbContext db, ISettlementService settlementService, IBalanceService balanceService)
    {
        _db = db;
        _settlementService = settlementService;
        _balanceService = balanceService;
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
    public async Task<GameDto?> GetGameByIdAsync(Guid gameId)
    {
        var game = await _db.Games
            .AsNoTracking()
            .Include(g => g.Markets)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        return game is null ? null : ToDto(game);
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

    /// <inheritdoc/>
    public async Task<GameDto> UpdateGameAsync(Guid gameId, UpdateGameRequest request)
    {
        var game = await _db.Games
            .Include(g => g.Markets)
                .ThenInclude(m => m.Bets)
            .FirstOrDefaultAsync(g => g.Id == gameId)
            ?? throw new KeyNotFoundException($"Game {gameId} not found.");

        if (game.Status != GameStatus.Scheduled)
            throw new InvalidOperationException("GAME_CANNOT_BE_EDITED");

        if (request.TeamA != null)
            game.TeamA = request.TeamA;

        if (request.TeamB != null)
            game.TeamB = request.TeamB;

        if (request.ScheduledAt != null)
            game.ScheduledAt = request.ScheduledAt.Value;

        if (request.NumberOfMaps != null)
        {
            var oldN = game.NumberOfMaps;
            var newN = request.NumberOfMaps.Value;

            if (oldN != newN)
            {
                // Remove map markets for maps that no longer exist (only those without bets)
                for (var map = newN + 1; map <= oldN; map++)
                {
                    var marketsToRemove = game.Markets
                        .Where(m => m.MapNumber == map && !m.Bets.Any())
                        .ToList();

                    foreach (var market in marketsToRemove)
                        _db.Markets.Remove(market);
                }

                // Add map markets for new maps
                for (var map = oldN + 1; map <= newN; map++)
                {
                    foreach (var type in MapMarketTypes)
                    {
                        var market = new Market
                        {
                            Id        = Guid.NewGuid(),
                            GameId    = game.Id,
                            Type      = type,
                            MapNumber = map,
                            Status    = MarketStatus.Open,
                        };
                        game.Markets.Add(market);
                    }
                }

                game.NumberOfMaps = newN;
            }
        }

        await _db.SaveChangesAsync();

        // Reload to get updated markets list
        await _db.Entry(game).Collection(g => g.Markets).LoadAsync();

        return ToDto(game);
    }

    /// <inheritdoc/>
    public async Task DeleteGameAsync(Guid gameId)
    {
        var game = await _db.Games
            .Include(g => g.Markets)
                .ThenInclude(m => m.Bets)
            .FirstOrDefaultAsync(g => g.Id == gameId)
            ?? throw new KeyNotFoundException($"Game {gameId} not found.");

        if (game.Status == GameStatus.InProgress || game.Status == GameStatus.Finished)
            throw new InvalidOperationException("GAME_CANNOT_BE_DELETED");

        foreach (var bet in game.Markets.SelectMany(m => m.Bets))
        {
            if (bet.Status == BetStatus.Pending)
            {
                await _balanceService.ReleaseBalanceAsync(bet.CreatorId, bet.Amount);
                bet.Status = BetStatus.Cancelled;
            }
            else if (bet.Status == BetStatus.Active)
            {
                await _balanceService.ReleaseBalanceAsync(bet.CreatorId, bet.Amount);
                await _balanceService.ReleaseBalanceAsync(bet.CoveredById!.Value, bet.Amount);
                bet.Status = BetStatus.Voided;
            }
        }

        _db.Games.Remove(game);
        await _db.SaveChangesAsync();
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
