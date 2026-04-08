namespace FrogBets.Api.Services;

public record CreateGameRequest(string TeamA, string TeamB, DateTime ScheduledAt, int NumberOfMaps);

public record RegisterResultRequest(Guid MarketId, string WinningOption, int? MapNumber);

public record UpdateGameRequest(
    string? TeamA,
    string? TeamB,
    DateTime? ScheduledAt,
    int? NumberOfMaps);

public interface IGameService
{
    /// <summary>
    /// Creates a new game and auto-generates markets for each map and the series.
    /// </summary>
    Task<Guid> CreateGameAsync(CreateGameRequest request);

    /// <summary>
    /// Returns all games ordered by ScheduledAt.
    /// </summary>
    Task<IReadOnlyList<GameDto>> GetGamesAsync();

    /// <summary>
    /// Returns a single game by ID, or null if not found.
    /// </summary>
    Task<GameDto?> GetGameByIdAsync(Guid gameId);

    /// <summary>
    /// Sets game status to InProgress and closes all Open markets.
    /// </summary>
    Task StartGameAsync(Guid gameId);

    /// <summary>
    /// Registers a market result. Throws InvalidOperationException("GAME_ALREADY_FINISHED") if game is Finished.
    /// Sets market WinningOption and Status = Settled.
    /// If all markets are Settled/Voided, sets game Status = Finished.
    /// </summary>
    Task RegisterResultAsync(Guid gameId, RegisterResultRequest request, Guid adminId);

    /// <summary>
    /// Atualiza campos de um jogo Scheduled. Regenera mercados se NumberOfMaps mudar.
    /// Throws KeyNotFoundException se o jogo não existir.
    /// Throws InvalidOperationException("GAME_CANNOT_BE_EDITED") se status != Scheduled.
    /// </summary>
    Task<GameDto> UpdateGameAsync(Guid gameId, UpdateGameRequest request);

    /// <summary>
    /// Exclui um jogo Scheduled, cancelando apostas e devolvendo saldo.
    /// Throws KeyNotFoundException se o jogo não existir.
    /// Throws InvalidOperationException("GAME_CANNOT_BE_DELETED") se status == InProgress ou Finished.
    /// </summary>
    Task DeleteGameAsync(Guid gameId);
}

public record GameDto(
    Guid Id,
    string TeamA,
    string TeamB,
    DateTime ScheduledAt,
    int NumberOfMaps,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<MarketDto> Markets);

public record MarketDto(
    Guid Id,
    string Type,
    int? MapNumber,
    string Status,
    string? WinningOption);
