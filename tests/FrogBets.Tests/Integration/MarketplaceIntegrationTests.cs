using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;

namespace FrogBets.Tests.Integration;

public class MarketplaceIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public MarketplaceIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private void AuthAs(Guid userId, string username)
    {
        var token = IntegrationTestFactory.GenerateToken(userId, username);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task GetMarketplace_Authenticated_Returns200()
    {
        using var db = _factory.CreateDbContext();
        var user = await IntegrationTestFactory.SeedUserAsync(db, "mp_user");
        AuthAs(user.Id, user.Username);

        var res = await _client.GetAsync("/api/marketplace");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetMarketplace_ExcludesOwnBets()
    {
        using var db = _factory.CreateDbContext();
        var creator = await IntegrationTestFactory.SeedUserAsync(db, "mp_creator", balance: 500m);
        var viewer  = await IntegrationTestFactory.SeedUserAsync(db, "mp_viewer", balance: 500m);

        var game = new Game
        {
            Id           = Guid.NewGuid(),
            TeamA        = "A",
            TeamB        = "B",
            ScheduledAt  = DateTime.UtcNow.AddDays(1),
            NumberOfMaps = 1,
            Status       = GameStatus.Scheduled,
            CreatedAt    = DateTime.UtcNow,
        };
        var market = new Market
        {
            Id        = Guid.NewGuid(),
            GameId    = game.Id,
            Type      = MarketType.MapWinner,
            MapNumber = 1,
            Status    = MarketStatus.Open,
            Game      = game,
        };
        game.Markets.Add(market);
        db.Games.Add(game);
        var bet = new Bet
        {
            Id            = Guid.NewGuid(),
            MarketId      = market.Id,
            CreatorId     = creator.Id,
            CreatorOption = "TeamA",
            Amount        = 100m,
            Status        = BetStatus.Pending,
            CreatedAt     = DateTime.UtcNow,
        };
        db.Bets.Add(bet);
        await db.SaveChangesAsync();

        // Creator should not see own bet
        AuthAs(creator.Id, creator.Username);
        var creatorRes = await _client.GetAsync("/api/marketplace");
        var creatorBody = await creatorRes.Content.ReadAsStringAsync();
        Assert.DoesNotContain(bet.Id.ToString(), creatorBody);

        // Viewer should see the bet
        AuthAs(viewer.Id, viewer.Username);
        var viewerRes = await _client.GetAsync("/api/marketplace");
        var viewerBody = await viewerRes.Content.ReadAsStringAsync();
        Assert.Contains(bet.Id.ToString(), viewerBody);
    }

    [Fact]
    public async Task GetMarketplace_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var res = await _client.GetAsync("/api/marketplace");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
