using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;

namespace FrogBets.Tests.Integration;

public class BetsIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public BetsIntegrationTests(IntegrationTestFactory factory)
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

    private async Task<(Game game, Market market)> SeedOpenGameAsync(string suffix = "")
    {
        using var db = _factory.CreateDbContext();
        var game = new Game
        {
            Id           = Guid.NewGuid(),
            TeamA        = $"TeamA{suffix}",
            TeamB        = $"TeamB{suffix}",
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
        await db.SaveChangesAsync();
        return (game, market);
    }

    // ── GET /api/bets ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBets_Authenticated_Returns200()
    {
        using var db = _factory.CreateDbContext();
        var user = await IntegrationTestFactory.SeedUserAsync(db, "bets_list_user");
        AuthAs(user.Id, user.Username);

        var res = await _client.GetAsync("/api/bets");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetBets_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var res = await _client.GetAsync("/api/bets");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ── POST /api/bets ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBet_ValidRequest_Returns201()
    {
        using var db = _factory.CreateDbContext();
        var user = await IntegrationTestFactory.SeedUserAsync(db, "bet_creator", balance: 500m);
        var (_, market) = await SeedOpenGameAsync("create");
        AuthAs(user.Id, user.Username);

        var res = await _client.PostAsJsonAsync("/api/bets", new
        {
            marketId      = market.Id,
            creatorOption = "TeamA",
            amount        = 100,
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task CreateBet_InsufficientBalance_Returns400()
    {
        using var db = _factory.CreateDbContext();
        var user = await IntegrationTestFactory.SeedUserAsync(db, "bet_broke", balance: 10m);
        var (_, market) = await SeedOpenGameAsync("broke");
        AuthAs(user.Id, user.Username);

        var res = await _client.PostAsJsonAsync("/api/bets", new
        {
            marketId      = market.Id,
            creatorOption = "TeamA",
            amount        = 100,
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("INSUFFICIENT_BALANCE", body);
    }

    [Fact]
    public async Task CreateBet_UnknownMarket_Returns404()
    {
        using var db = _factory.CreateDbContext();
        var user = await IntegrationTestFactory.SeedUserAsync(db, "bet_nomarket");
        AuthAs(user.Id, user.Username);

        var res = await _client.PostAsJsonAsync("/api/bets", new
        {
            marketId      = Guid.NewGuid(),
            creatorOption = "TeamA",
            amount        = 100,
        });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ── POST /api/bets/:id/cover ──────────────────────────────────────────────

    [Fact]
    public async Task CoverBet_ValidRequest_Returns200()
    {
        using var db = _factory.CreateDbContext();
        var creator = await IntegrationTestFactory.SeedUserAsync(db, "cover_creator", balance: 500m);
        var coverer = await IntegrationTestFactory.SeedUserAsync(db, "cover_coverer", balance: 500m);
        var (_, market) = await SeedOpenGameAsync("cover");

        // Create bet as creator
        AuthAs(creator.Id, creator.Username);
        var createRes = await _client.PostAsJsonAsync("/api/bets", new
        {
            marketId      = market.Id,
            creatorOption = "TeamA",
            amount        = 100,
        });
        var betBody = await createRes.Content.ReadFromJsonAsync<BetCreatedResponse>();

        // Cover as coverer
        AuthAs(coverer.Id, coverer.Username);
        var coverRes = await _client.PostAsync($"/api/bets/{betBody!.Id}/cover", null);

        Assert.Equal(HttpStatusCode.OK, coverRes.StatusCode);
    }

    [Fact]
    public async Task CoverBet_OwnBet_Returns400()
    {
        using var db = _factory.CreateDbContext();
        var user = await IntegrationTestFactory.SeedUserAsync(db, "cover_self", balance: 500m);
        var (_, market) = await SeedOpenGameAsync("self");
        AuthAs(user.Id, user.Username);

        var createRes = await _client.PostAsJsonAsync("/api/bets", new
        {
            marketId      = market.Id,
            creatorOption = "TeamA",
            amount        = 100,
        });
        var betBody = await createRes.Content.ReadFromJsonAsync<BetCreatedResponse>();

        var coverRes = await _client.PostAsync($"/api/bets/{betBody!.Id}/cover", null);

        Assert.Equal(HttpStatusCode.BadRequest, coverRes.StatusCode);
        var body = await coverRes.Content.ReadAsStringAsync();
        Assert.Contains("CANNOT_COVER_OWN_BET", body);
    }

    // ── DELETE /api/bets/:id ──────────────────────────────────────────────────

    [Fact]
    public async Task CancelBet_OwnPendingBet_Returns204()
    {
        using var db = _factory.CreateDbContext();
        var user = await IntegrationTestFactory.SeedUserAsync(db, "cancel_user", balance: 500m);
        var (_, market) = await SeedOpenGameAsync("cancel");
        AuthAs(user.Id, user.Username);

        var createRes = await _client.PostAsJsonAsync("/api/bets", new
        {
            marketId      = market.Id,
            creatorOption = "TeamA",
            amount        = 100,
        });
        var betBody = await createRes.Content.ReadFromJsonAsync<BetCreatedResponse>();

        var cancelRes = await _client.DeleteAsync($"/api/bets/{betBody!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, cancelRes.StatusCode);
    }

    [Fact]
    public async Task CancelBet_OtherUsersBet_Returns400()
    {
        using var db = _factory.CreateDbContext();
        var creator = await IntegrationTestFactory.SeedUserAsync(db, "cancel_creator2", balance: 500m);
        var other   = await IntegrationTestFactory.SeedUserAsync(db, "cancel_other2", balance: 500m);
        var (_, market) = await SeedOpenGameAsync("cancel2");

        AuthAs(creator.Id, creator.Username);
        var createRes = await _client.PostAsJsonAsync("/api/bets", new
        {
            marketId      = market.Id,
            creatorOption = "TeamA",
            amount        = 100,
        });
        var betBody = await createRes.Content.ReadFromJsonAsync<BetCreatedResponse>();

        AuthAs(other.Id, other.Username);
        var cancelRes = await _client.DeleteAsync($"/api/bets/{betBody!.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, cancelRes.StatusCode);
        var body = await cancelRes.Content.ReadAsStringAsync();
        Assert.Contains("NOT_BET_OWNER", body);
    }

    private record BetCreatedResponse(Guid Id, Guid MarketId, string CreatorOption, decimal Amount, string Status);
}
