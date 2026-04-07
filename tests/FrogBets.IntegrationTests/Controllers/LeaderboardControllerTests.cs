using System.Net;
using System.Net.Http.Json;
using FrogBets.IntegrationTests.Helpers;
using Xunit;

namespace FrogBets.IntegrationTests.Controllers;

public class LeaderboardControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public LeaderboardControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetLeaderboard_Authenticated_Returns200()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var user   = await SeedHelper.SeedUserAsync(db);
        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, user.Id, user.Username);

        // Act
        var res = await client.GetAsync("/api/leaderboard");

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetLeaderboard_Unauthenticated_Returns401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/leaderboard");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task GetLeaderboard_ReturnsUsersOrderedByBalance()
    {
        // Arrange
        using var db = SeedHelper.GetDb(_factory);
        var rich = await SeedHelper.SeedUserAsync(db, virtualBalance: 2000m);
        var poor = await SeedHelper.SeedUserAsync(db, virtualBalance: 100m);
        var mid  = await SeedHelper.SeedUserAsync(db, virtualBalance: 500m);

        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, rich.Id, rich.Username);

        // Act
        var res = await client.GetAsync("/api/leaderboard");
        var entries = await res.Content.ReadFromJsonAsync<List<LeaderboardEntry>>();

        // Assert
        Assert.NotNull(entries);
        // First entry should have the highest balance
        Assert.True(entries![0].virtualBalance >= entries[1].virtualBalance);
    }

    private record LeaderboardEntry(string username, decimal virtualBalance, int winsCount, int lossesCount);
}
