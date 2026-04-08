using FrogBets.Api.Services;
using FrogBets.Domain.Entities;
using FrogBets.Infrastructure.Data;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FrogBets.Tests;

/// <summary>
/// Tests for the user-player-unification spec.
/// </summary>
public class UserPlayerUnificationTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static FrogBetsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FrogBetsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new FrogBetsDbContext(options);
    }

    private static async Task<CS2Team> SeedTeamAsync(FrogBetsDbContext db, string? name = null)
    {
        var team = new CS2Team
        {
            Id        = Guid.NewGuid(),
            Name      = name ?? Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
        };
        db.CS2Teams.Add(team);
        await db.SaveChangesAsync();
        return team;
    }

    private static async Task<User> SeedUserAsync(FrogBetsDbContext db, string? username = null)
    {
        var user = new User
        {
            Id              = Guid.NewGuid(),
            Username        = username ?? Guid.NewGuid().ToString("N"),
            PasswordHash    = "hash",
            IsAdmin         = false,
            VirtualBalance  = 1000m,
            ReservedBalance = 0m,
            CreatedAt       = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<CS2Player> SeedPlayerAsync(FrogBetsDbContext db, Guid teamId, Guid? userId = null, string? nickname = null)
    {
        var player = new CS2Player
        {
            Id           = Guid.NewGuid(),
            Nickname     = nickname ?? Guid.NewGuid().ToString("N"),
            TeamId       = teamId,
            UserId       = userId,
            PlayerScore  = 0.0,
            MatchesCount = 0,
            CreatedAt    = DateTime.UtcNow,
        };
        db.CS2Players.Add(player);
        await db.SaveChangesAsync();
        return player;
    }

    // ── 2.3 Unit tests for PlayerService.GetPlayersAsync() ────────────────────

    [Fact]
    public async Task GetPlayersAsync_PlayerWithUserId_ReturnsUsernameInDto()
    {
        await using var db = CreateDb();
        var team   = await SeedTeamAsync(db);
        var user   = await SeedUserAsync(db, "linked_user");
        await SeedPlayerAsync(db, team.Id, userId: user.Id, nickname: "linked_player");

        var svc     = new PlayerService(db);
        var players = await svc.GetPlayersAsync();

        var dto = Assert.Single(players);
        Assert.Equal("linked_user", dto.Username);
    }

    [Fact]
    public async Task GetPlayersAsync_LegacyPlayerWithoutUserId_ReturnsNullUsername()
    {
        await using var db = CreateDb();
        var team = await SeedTeamAsync(db);
        await SeedPlayerAsync(db, team.Id, userId: null, nickname: "legacy_player");

        var svc     = new PlayerService(db);
        var players = await svc.GetPlayersAsync();

        var dto = Assert.Single(players);
        Assert.Null(dto.Username);
    }

    [Fact]
    public async Task GetPlayersAsync_MixedPlayers_ReturnsAll()
    {
        await using var db = CreateDb();
        var team   = await SeedTeamAsync(db);
        var user   = await SeedUserAsync(db, "user_a");
        await SeedPlayerAsync(db, team.Id, userId: user.Id, nickname: "player_a");
        await SeedPlayerAsync(db, team.Id, userId: null, nickname: "player_b");

        var svc     = new PlayerService(db);
        var players = await svc.GetPlayersAsync();

        Assert.Equal(2, players.Count);
        Assert.Contains(players, p => p.Username == "user_a");
        Assert.Contains(players, p => p.Username == null);
    }

    // ── 2.4 Property test: CS2Player vinculado expõe Username no DTO ─────────

    // Feature: user-player-unification, Property 2: CS2Player vinculado expõe Username no DTO
    // Validates: Requirements 2.4, 4.3
    [Property(MaxTest = 50)]
    public Property GetPlayersAsync_LinkedPlayer_ExposesUsername()
    {
        var gen = Gen.Choose(1, 5);

        return Prop.ForAll(gen.ToArbitrary(), count =>
        {
            using var db = CreateDb();
            var team = SeedTeamAsync(db).GetAwaiter().GetResult();
            var svc  = new PlayerService(db);

            var expectedUsernames = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var uname = $"user_{i}_{Guid.NewGuid():N}";
                var user  = SeedUserAsync(db, uname).GetAwaiter().GetResult();
                SeedPlayerAsync(db, team.Id, userId: user.Id, nickname: $"player_{i}_{Guid.NewGuid():N}").GetAwaiter().GetResult();
                expectedUsernames.Add(uname);
            }

            var players = svc.GetPlayersAsync().GetAwaiter().GetResult();

            return players.Count == count
                && players.All(p => p.Username != null)
                && expectedUsernames.All(u => players.Any(p => p.Username == u));
        });
    }

    // ── 2.5 Property test: jogadores legados aparecem nas listagens ───────────

    // Feature: user-player-unification, Property 4: jogadores legados aparecem nas listagens
    // Validates: Requirements 5.2, 5.3
    [Property(MaxTest = 50)]
    public Property GetPlayersAsync_LegacyPlayers_AppearInListings()
    {
        var gen = Gen.Choose(1, 5);

        return Prop.ForAll(gen.ToArbitrary(), legacyCount =>
        {
            using var db = CreateDb();
            var team = SeedTeamAsync(db).GetAwaiter().GetResult();
            var svc  = new PlayerService(db);

            for (int i = 0; i < legacyCount; i++)
                SeedPlayerAsync(db, team.Id, userId: null, nickname: $"legacy_{i}_{Guid.NewGuid():N}").GetAwaiter().GetResult();

            var players = svc.GetPlayersAsync().GetAwaiter().GetResult();
            var ranking = svc.GetRankingAsync().GetAwaiter().GetResult();

            return players.Count == legacyCount
                && ranking.Count == legacyCount
                && players.All(p => p.Username == null);
        });
    }

    // ── 3.2 Unit tests for AuthService.RegisterAsync ─────────────────────────

    [Fact]
    public async Task RegisterAsync_WithTeamId_CreatesCS2Player()
    {
        await using var db = CreateDb();
        var team = await SeedTeamAsync(db, "TestTeam");

        var invite = new Invite
        {
            Id          = Guid.NewGuid(),
            Token       = Guid.NewGuid().ToString("N")[..32],
            ExpiresAt   = DateTime.UtcNow.AddDays(1),
            CreatedAt   = DateTime.UtcNow,
        };
        db.Invites.Add(invite);
        await db.SaveChangesAsync();

        var config  = BuildConfig();
        var svc     = new AuthService(db, config, CreateBlocklist(db));
        await svc.RegisterAsync("testuser", "password123", invite.Id, team.Id);

        var user   = await db.Users.FirstAsync(u => u.Username == "testuser");
        var player = await db.CS2Players.FirstOrDefaultAsync(p => p.UserId == user.Id);

        Assert.NotNull(player);
        Assert.Equal("testuser", player!.Nickname);
        Assert.Equal(user.Id, player.UserId);
        Assert.Equal(0.0, player.PlayerScore);
        Assert.Equal(0, player.MatchesCount);
    }

    [Fact]
    public async Task RegisterAsync_WithoutTeamId_DoesNotCreateCS2Player()
    {
        await using var db = CreateDb();

        var invite = new Invite
        {
            Id          = Guid.NewGuid(),
            Token       = Guid.NewGuid().ToString("N")[..32],
            ExpiresAt   = DateTime.UtcNow.AddDays(1),
            CreatedAt   = DateTime.UtcNow,
        };
        db.Invites.Add(invite);
        await db.SaveChangesAsync();

        var config = BuildConfig();
        var svc    = new AuthService(db, config, CreateBlocklist(db));
        await svc.RegisterAsync("noTeamUser", "password123", invite.Id, null);

        var user    = await db.Users.FirstAsync(u => u.Username == "noTeamUser");
        var players = await db.CS2Players.Where(p => p.UserId == user.Id).ToListAsync();

        Assert.Empty(players);
    }

    // ── 3.3 Property test: registro cria CS2Player com dados corretos ─────────

    // Feature: user-player-unification, Property 1: registro cria CS2Player com dados corretos
    // Validates: Requirements 1.1, 1.2, 1.4, 1.5
    [Property(MaxTest = 30)]
    public Property RegisterAsync_WithTeam_CreatesCorrectCS2Player()
    {
        var gen = from suffix in Gen.Choose(1000, 9999)
                  select suffix;

        return Prop.ForAll(gen.ToArbitrary(), suffix =>
        {
            using var db = CreateDb();
            var team = SeedTeamAsync(db).GetAwaiter().GetResult();

            var invite = new Invite
            {
                Id        = Guid.NewGuid(),
                Token     = Guid.NewGuid().ToString("N")[..32],
                ExpiresAt = DateTime.UtcNow.AddDays(1),
                CreatedAt = DateTime.UtcNow,
            };
            db.Invites.Add(invite);
            db.SaveChanges();

            var username = $"user_{suffix}";
            var config   = BuildConfig();
            var svc      = new AuthService(db, config, CreateBlocklist(db));
            svc.RegisterAsync(username, "password123", invite.Id, team.Id).GetAwaiter().GetResult();

            var user   = db.Users.First(u => u.Username == username);
            var player = db.CS2Players.FirstOrDefault(p => p.UserId == user.Id);

            return player != null
                && player.Nickname == username
                && player.UserId == user.Id
                && player.PlayerScore == 0.0
                && player.MatchesCount == 0;
        });
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static TokenBlocklist CreateBlocklist(FrogBetsDbContext db)
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<FrogBetsDbContext>(o => o.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        return new TokenBlocklist(scopeFactory);
    }

    private static IConfiguration BuildConfig()
    {
        var dict = new Dictionary<string, string?>
        {
            ["Jwt:Key"]               = "TESTKEY_MUST_BE_AT_LEAST_32_CHARS_LONG!",
            ["Jwt:Issuer"]            = "test",
            ["Jwt:Audience"]          = "test",
            ["Jwt:ExpirationMinutes"] = "60",
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }
}
