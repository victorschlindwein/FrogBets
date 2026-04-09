using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;
using FrogBets.Infrastructure.Data;
using FrogBets.Tests.Integration;

namespace FrogBets.Tests;

/// <summary>
/// Bug Condition Exploration Tests — Bug 2: CS2Players faltando no endpoint GET /api/games/{id}/players
///
/// Validates: Requirements 1.3, 1.4
///
/// Estes testes confirmam o bug: o endpoint busca Users onde TeamId está nos times do jogo,
/// mas CS2Players sem UserId vinculado ficam de fora.
///
/// EXPECTED OUTCOME: Testes FALHAM no código atual retornando apenas os usuários com TeamId
/// (2 em vez de 4 CS2Players).
/// </summary>
public class MarketplacePlayerBugConditionTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public MarketplacePlayerBugConditionTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private void AuthAs(Guid userId, string username, bool isAdmin = false)
    {
        var token = IntegrationTestFactory.GenerateToken(userId, username, isAdmin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Bug Condition: jogo com 4 CS2Players (2 por time, 1 com UserId=null por time)
    /// deve retornar todos os 4 jogadores.
    ///
    /// EXPECTED OUTCOME no código atual: retorna apenas 2 (os que têm UserId vinculado),
    /// ignorando os 2 CS2Players com UserId=null.
    ///
    /// Counterexample: response.Count == 2 quando deveria ser 4.
    /// </summary>
    [Fact]
    public async Task GetGamePlayers_WithCS2PlayersWithoutUserId_ReturnsAllFourPlayers()
    {
        using var db = _factory.CreateDbContext();

        // Seed: usuário autenticado
        var authUser = await IntegrationTestFactory.SeedUserAsync(db, "auth_user_bugcond");
        AuthAs(authUser.Id, authUser.Username);

        // Seed: dois times CS2
        var teamA = new CS2Team
        {
            Id        = Guid.NewGuid(),
            Name      = "TeamAlpha",
            CreatedAt = DateTime.UtcNow,
        };
        var teamB = new CS2Team
        {
            Id        = Guid.NewGuid(),
            Name      = "TeamBeta",
            CreatedAt = DateTime.UtcNow,
        };
        db.CS2Teams.AddRange(teamA, teamB);

        // Seed: jogo com os dois times
        var game = new Game
        {
            Id           = Guid.NewGuid(),
            TeamA        = teamA.Name,
            TeamB        = teamB.Name,
            ScheduledAt  = DateTime.UtcNow.AddDays(1),
            NumberOfMaps = 3,
            Status       = GameStatus.Scheduled,
            CreatedAt    = DateTime.UtcNow,
        };
        db.Games.Add(game);

        // Seed: 2 CS2Players em TeamA
        // Player 1: com UserId vinculado (User com TeamId)
        var userA1 = new User
        {
            Id              = Guid.NewGuid(),
            Username        = "player_a1_user",
            PasswordHash    = "hash",
            VirtualBalance  = 1000m,
            ReservedBalance = 0m,
            TeamId          = teamA.Id,
            CreatedAt       = DateTime.UtcNow,
        };
        db.Users.Add(userA1);

        var playerA1 = new CS2Player
        {
            Id        = Guid.NewGuid(),
            TeamId    = teamA.Id,
            Nickname  = "PlayerA1",
            UserId    = userA1.Id,
            CreatedAt = DateTime.UtcNow,
        };

        // Player 2: sem UserId (CS2Player puro, sem User correspondente)
        var playerA2 = new CS2Player
        {
            Id        = Guid.NewGuid(),
            TeamId    = teamA.Id,
            Nickname  = "PlayerA2",
            UserId    = null, // Bug condition: sem UserId
            CreatedAt = DateTime.UtcNow,
        };

        // Seed: 2 CS2Players em TeamB
        // Player 3: com UserId vinculado
        var userB1 = new User
        {
            Id              = Guid.NewGuid(),
            Username        = "player_b1_user",
            PasswordHash    = "hash",
            VirtualBalance  = 1000m,
            ReservedBalance = 0m,
            TeamId          = teamB.Id,
            CreatedAt       = DateTime.UtcNow,
        };
        db.Users.Add(userB1);

        var playerB1 = new CS2Player
        {
            Id        = Guid.NewGuid(),
            TeamId    = teamB.Id,
            Nickname  = "PlayerB1",
            UserId    = userB1.Id,
            CreatedAt = DateTime.UtcNow,
        };

        // Player 4: sem UserId (CS2Player puro, sem User correspondente)
        var playerB2 = new CS2Player
        {
            Id        = Guid.NewGuid(),
            TeamId    = teamB.Id,
            Nickname  = "PlayerB2",
            UserId    = null, // Bug condition: sem UserId
            CreatedAt = DateTime.UtcNow,
        };

        db.CS2Players.AddRange(playerA1, playerA2, playerB1, playerB2);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/games/{game.Id}/players");

        // Assert: deve retornar 200
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        // Assert: deve conter todos os 4 CS2Players
        // Bug: no código atual, retorna apenas 2 (PlayerA1 e PlayerB1, que têm UserId)
        // Counterexample: body contém apenas 2 jogadores em vez de 4
        Assert.Contains("PlayerA1", body);
        Assert.Contains("PlayerA2", body); // FALHA no código atual — CS2Player sem UserId
        Assert.Contains("PlayerB1", body);
        Assert.Contains("PlayerB2", body); // FALHA no código atual — CS2Player sem UserId
    }

    /// <summary>
    /// Bug Condition: verifica que a contagem de jogadores retornados é exatamente 4.
    /// Confirma que o bug resulta em contagem incorreta (2 em vez de 4).
    /// </summary>
    [Fact]
    public async Task GetGamePlayers_WithCS2PlayersWithoutUserId_CountIsExactlyFour()
    {
        using var db = _factory.CreateDbContext();

        var authUser = await IntegrationTestFactory.SeedUserAsync(db, "auth_user_count");
        AuthAs(authUser.Id, authUser.Username);

        var teamA = new CS2Team { Id = Guid.NewGuid(), Name = "CountTeamA", CreatedAt = DateTime.UtcNow };
        var teamB = new CS2Team { Id = Guid.NewGuid(), Name = "CountTeamB", CreatedAt = DateTime.UtcNow };
        db.CS2Teams.AddRange(teamA, teamB);

        var game = new Game
        {
            Id           = Guid.NewGuid(),
            TeamA        = teamA.Name,
            TeamB        = teamB.Name,
            ScheduledAt  = DateTime.UtcNow.AddDays(1),
            NumberOfMaps = 1,
            Status       = GameStatus.Scheduled,
            CreatedAt    = DateTime.UtcNow,
        };
        db.Games.Add(game);

        // 2 users com TeamId (para simular o comportamento atual da query bugada)
        var userA = new User { Id = Guid.NewGuid(), Username = "cnt_ua", PasswordHash = "h", VirtualBalance = 100m, ReservedBalance = 0m, TeamId = teamA.Id, CreatedAt = DateTime.UtcNow };
        var userB = new User { Id = Guid.NewGuid(), Username = "cnt_ub", PasswordHash = "h", VirtualBalance = 100m, ReservedBalance = 0m, TeamId = teamB.Id, CreatedAt = DateTime.UtcNow };
        db.Users.AddRange(userA, userB);

        // 4 CS2Players: 1 com UserId por time, 1 sem UserId por time
        db.CS2Players.AddRange(
            new CS2Player { Id = Guid.NewGuid(), TeamId = teamA.Id, Nickname = "CntA1", UserId = userA.Id, CreatedAt = DateTime.UtcNow },
            new CS2Player { Id = Guid.NewGuid(), TeamId = teamA.Id, Nickname = "CntA2", UserId = null, CreatedAt = DateTime.UtcNow },
            new CS2Player { Id = Guid.NewGuid(), TeamId = teamB.Id, Nickname = "CntB1", UserId = userB.Id, CreatedAt = DateTime.UtcNow },
            new CS2Player { Id = Guid.NewGuid(), TeamId = teamB.Id, Nickname = "CntB2", UserId = null, CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/api/games/{game.Id}/players");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var players = await response.Content.ReadFromJsonAsync<List<PlayerDto>>();
        Assert.NotNull(players);

        // Bug condition: no código atual retorna 2 (apenas Users com TeamId)
        // Comportamento esperado: retorna 4 (todos os CS2Players dos times)
        // EXPECTED OUTCOME: este Assert FALHA no código atual (players.Count == 2, não 4)
        Assert.Equal(4, players!.Count);
    }

    private record PlayerDto(Guid id, string nickname, string teamName);
}
