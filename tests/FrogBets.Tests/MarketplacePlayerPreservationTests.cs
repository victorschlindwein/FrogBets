using System.Net;
using System.Net.Http.Headers;
using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;
using FrogBets.Tests.Integration;
using FsCheck;
using FsCheck.Xunit;

namespace FrogBets.Tests;

/// <summary>
/// Preservation Tests — Bug 2: Comportamento baseline do endpoint GET /api/games/{id}/players
///
/// Validates: Requirements 3.5
///
/// Estes testes confirmam comportamentos que devem ser PRESERVADOS após o fix.
/// Para qualquer GUID não existente no banco, o endpoint deve retornar 404 com GAME_NOT_FOUND.
///
/// EXPECTED OUTCOME: Testes PASSAM no código atual (não corrigido).
/// </summary>
public class MarketplacePlayerPreservationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public MarketplacePlayerPreservationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        // Autenticar com um usuário fixo para todos os testes desta classe
        var userId = Guid.NewGuid();
        var token = IntegrationTestFactory.GenerateToken(userId, "preservation_user");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Preservation: para qualquer GUID aleatório não inserido no banco,
    /// GET /api/games/{id}/players retorna 404 com GAME_NOT_FOUND.
    ///
    /// Validates: Requirement 3.5
    ///
    /// Property 4: Preservation — Comportamento do endpoint para jogo inexistente
    /// _For any_ gameId que não corresponda a um jogo existente no banco,
    /// GET /api/games/{id}/players SHALL continuar retornando 404 com GAME_NOT_FOUND.
    ///
    /// EXPECTED OUTCOME: PASSA no código atual (não corrigido).
    /// </summary>
    [Property(MaxTest = 20)]
    public Property GetGamePlayers_ForAnyRandomGuidNotInDb_Returns404WithGameNotFound()
    {
        return Prop.ForAll(Arb.Default.Guid(), guid =>
        {
            // Executar de forma síncrona (FsCheck não suporta async nativamente)
            var response = _client.GetAsync($"/api/games/{guid}/players").GetAwaiter().GetResult();

            if (response.StatusCode != HttpStatusCode.NotFound)
                return false;

            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return body.Contains("GAME_NOT_FOUND");
        });
    }

    /// <summary>
    /// Preservation (exemplo concreto): GUID específico não existente retorna 404.
    /// Complementa o property test com um caso determinístico.
    ///
    /// Validates: Requirement 3.5
    /// EXPECTED OUTCOME: PASSA no código atual (não corrigido).
    /// </summary>
    [Fact]
    public async Task GetGamePlayers_NonExistentGame_Returns404WithGameNotFound()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/games/{nonExistentId}/players");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("GAME_NOT_FOUND", body);
    }
}
