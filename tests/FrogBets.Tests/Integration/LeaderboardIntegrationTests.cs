using System.Net;
using System.Net.Http.Headers;

namespace FrogBets.Tests.Integration;

public class LeaderboardIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public LeaderboardIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetLeaderboard_Authenticated_Returns200()
    {
        using var db = _factory.CreateDbContext();
        var user = await IntegrationTestFactory.SeedUserAsync(db, "lb_user");
        var token = IntegrationTestFactory.GenerateToken(user.Id, user.Username);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/api/leaderboard");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetLeaderboard_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var res = await _client.GetAsync("/api/leaderboard");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task GetLeaderboard_OrderedByBalanceDescending()
    {
        using var db = _factory.CreateDbContext();
        var u1 = await IntegrationTestFactory.SeedUserAsync(db, "lb_rich", balance: 2000m);
        var u2 = await IntegrationTestFactory.SeedUserAsync(db, "lb_poor", balance: 500m);
        var token = IntegrationTestFactory.GenerateToken(u1.Id, u1.Username);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/api/leaderboard");
        var body = await res.Content.ReadAsStringAsync();

        // lb_rich (2000) should appear before lb_poor (500)
        var richIdx = body.IndexOf("lb_rich", StringComparison.Ordinal);
        var poorIdx = body.IndexOf("lb_poor", StringComparison.Ordinal);
        Assert.True(richIdx < poorIdx);
    }
}
