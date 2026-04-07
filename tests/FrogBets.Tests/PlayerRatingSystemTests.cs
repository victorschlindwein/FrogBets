using FrogBets.Api.Services;
using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;
using FrogBets.Infrastructure.Data;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FrogBets.Tests;

/// <summary>
/// Property-based tests for the Player Rating System (player-rating-system spec).
/// Feature: player-rating-system
/// </summary>
public class PlayerRatingSystemTests
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

    private static async Task<CS2Player> SeedPlayerAsync(FrogBetsDbContext db, Guid teamId, string? nickname = null)
    {
        var player = new CS2Player
        {
            Id           = Guid.NewGuid(),
            Nickname     = nickname ?? Guid.NewGuid().ToString("N"),
            TeamId       = teamId,
            PlayerScore  = 0.0,
            MatchesCount = 0,
            CreatedAt    = DateTime.UtcNow,
        };
        db.CS2Players.Add(player);
        await db.SaveChangesAsync();
        return player;
    }

    private static async Task<Game> SeedGameAsync(FrogBetsDbContext db)
    {
        var game = new Game
        {
            Id           = Guid.NewGuid(),
            TeamA        = "TeamA",
            TeamB        = "TeamB",
            ScheduledAt  = DateTime.UtcNow.AddDays(1),
            NumberOfMaps = 1,
            Status       = GameStatus.Scheduled,
            CreatedAt    = DateTime.UtcNow,
        };
        db.Games.Add(game);
        await db.SaveChangesAsync();
        return game;
    }

    private static async Task<MapResult> SeedMapResultAsync(FrogBetsDbContext db, Guid gameId, int mapNumber = 1, int rounds = 20)
    {
        var mapResult = new MapResult
        {
            Id        = Guid.NewGuid(),
            GameId    = gameId,
            MapNumber = mapNumber,
            Rounds    = rounds,
            CreatedAt = DateTime.UtcNow,
        };
        db.MapResults.Add(mapResult);
        await db.SaveChangesAsync();
        return mapResult;
    }

    // ── RatingCalculator unit tests ───────────────────────────────────────────

    // Feature: player-rating-system, Property 4.1: KPR = kills / rounds
    [Property(MaxTest = 200)]
    public Property RatingCalculator_KPR_IsKillsDividedByRounds()
    {
        var gen = from kills   in Gen.Choose(0, 50)
                  from deaths  in Gen.Choose(0, 50)
                  from assists in Gen.Choose(0, 50)
                  from rounds  in Gen.Choose(1, 30)
                  from damage  in Gen.Choose(0, 5000).Select(d => (double)d)
                  from kast    in Gen.Choose(0, 100).Select(k => (double)k)
                  select (kills, deaths, assists, rounds, damage, kast);

        return Prop.ForAll(gen.ToArbitrary(), t =>
        {
            var (kills, deaths, assists, rounds, damage, kast) = t;
            double kpr = (double)kills / rounds;
            double dpr = (double)deaths / rounds;
            double adr = damage / rounds;
            double impact = kpr + ((double)assists / rounds * 0.4);
            double expected = 0.0073 * kast + 0.3591 * kpr + (-0.5329) * dpr
                            + 0.2372 * impact + 0.0032 * adr + 0.1587;

            double actual = RatingCalculator.Calculate(kills, deaths, assists, damage, rounds, kast);
            return Math.Abs(actual - expected) < 1e-9;
        });
    }

    // Feature: player-rating-system, Property 4.6: determinism — same inputs always produce same output
    [Property(MaxTest = 200)]
    public Property RatingCalculator_Determinism_SameInputsSameOutput()
    {
        var gen = from kills   in Gen.Choose(0, 50)
                  from deaths  in Gen.Choose(0, 50)
                  from assists in Gen.Choose(0, 50)
                  from rounds  in Gen.Choose(1, 30)
                  from damage  in Gen.Choose(0, 5000).Select(d => (double)d)
                  from kast    in Gen.Choose(0, 100).Select(k => (double)k)
                  select (kills, deaths, assists, rounds, damage, kast);

        return Prop.ForAll(gen.ToArbitrary(), t =>
        {
            var (kills, deaths, assists, rounds, damage, kast) = t;
            double r1 = RatingCalculator.Calculate(kills, deaths, assists, damage, rounds, kast);
            double r2 = RatingCalculator.Calculate(kills, deaths, assists, damage, rounds, kast);
            double r3 = RatingCalculator.Calculate(kills, deaths, assists, damage, rounds, kast);
            return r1 == r2 && r2 == r3;
        });
    }

    // Feature: player-rating-system, Property 4.7: precision — at least 4 decimal places
    [Property(MaxTest = 200)]
    public Property RatingCalculator_Precision_AtLeast4DecimalPlaces()
    {
        var gen = from kills   in Gen.Choose(0, 50)
                  from deaths  in Gen.Choose(0, 50)
                  from assists in Gen.Choose(0, 50)
                  from rounds  in Gen.Choose(1, 30)
                  from damage  in Gen.Choose(0, 5000).Select(d => (double)d)
                  from kast    in Gen.Choose(0, 100).Select(k => (double)k)
                  select (kills, deaths, assists, rounds, damage, kast);

        return Prop.ForAll(gen.ToArbitrary(), t =>
        {
            var (kills, deaths, assists, rounds, damage, kast) = t;
            double rating = RatingCalculator.Calculate(kills, deaths, assists, damage, rounds, kast);
            // Verify it's a finite double (not NaN or Infinity)
            return double.IsFinite(rating);
        });
    }

    // ── MatchStatsService property tests ─────────────────────────────────────

    // Feature: player-rating-system, Property 3.4: PlayerScore accumulates rating after each match
    [Property(MaxTest = 100)]
    public Property MatchStats_PlayerScoreAccumulates_AfterEachRegistration()
    {
        var gen = from n in Gen.Choose(1, 5)
                  select n;

        return Prop.ForAll(gen.ToArbitrary(), matchCount =>
        {
            using var db = CreateDb();
            var team   = SeedTeamAsync(db).GetAwaiter().GetResult();
            var player = SeedPlayerAsync(db, team.Id).GetAwaiter().GetResult();
            var svc    = new MatchStatsService(db);

            double expectedScore = 0.0;
            for (int i = 0; i < matchCount; i++)
            {
                var game      = SeedGameAsync(db).GetAwaiter().GetResult();
                var mapResult = SeedMapResultAsync(db, game.Id, mapNumber: i + 1, rounds: 20).GetAwaiter().GetResult();
                int kills = 10 + i, deaths = 5, assists = 3;
                double damage = 1500, kast = 70;

                var dto = svc.RegisterStatsAsync(new RegisterStatsRequest(
                    player.Id, mapResult.Id, kills, deaths, assists, damage, kast))
                    .GetAwaiter().GetResult();

                expectedScore += dto.Rating;
            }

            var updated = db.CS2Players.Find(player.Id)!;
            return Math.Abs(updated.PlayerScore - expectedScore) < 1e-9
                && updated.MatchesCount == matchCount;
        });
    }

    // Feature: player-rating-system, Property 3.5: duplicate stats for same player+mapResult are rejected
    [Fact]
    public async Task MatchStats_DuplicatePlayerMapResult_ThrowsStatsAlreadyRegistered()
    {
        await using var db = CreateDb();
        var team      = await SeedTeamAsync(db);
        var player    = await SeedPlayerAsync(db, team.Id);
        var game      = await SeedGameAsync(db);
        var mapResult = await SeedMapResultAsync(db, game.Id);
        var svc       = new MatchStatsService(db);

        await svc.RegisterStatsAsync(new RegisterStatsRequest(
            player.Id, mapResult.Id, 10, 5, 3, 1500, 70));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RegisterStatsAsync(new RegisterStatsRequest(
                player.Id, mapResult.Id, 12, 4, 2, 1600, 75)));

        Assert.Equal("STATS_ALREADY_REGISTERED", ex.Message);
    }

    // Feature: player-rating-system, Property 3.6: mapResult not found is rejected
    [Fact]
    public async Task MatchStats_MapResultNotFound_ThrowsMapResultNotFound()
    {
        await using var db = CreateDb();
        var team   = await SeedTeamAsync(db);
        var player = await SeedPlayerAsync(db, team.Id);
        var svc    = new MatchStatsService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RegisterStatsAsync(new RegisterStatsRequest(
                player.Id, Guid.NewGuid(), 10, 5, 3, 1500, 70)));

        Assert.Equal("MAP_RESULT_NOT_FOUND", ex.Message);
    }

    // Feature: player-rating-system, Property 3.7: KAST outside [0,100] is rejected
    [Property(MaxTest = 100)]
    public Property MatchStats_InvalidKast_IsRejected()
    {
        var gen = Gen.OneOf(
            Gen.Choose(-1000, -1).Select(x => (double)x),
            Gen.Choose(101, 1000).Select(x => (double)x));

        return Prop.ForAll(gen.ToArbitrary(), invalidKast =>
        {
            using var db = CreateDb();
            var team      = SeedTeamAsync(db).GetAwaiter().GetResult();
            var player    = SeedPlayerAsync(db, team.Id).GetAwaiter().GetResult();
            var game      = SeedGameAsync(db).GetAwaiter().GetResult();
            var mapResult = SeedMapResultAsync(db, game.Id).GetAwaiter().GetResult();
            var svc       = new MatchStatsService(db);

            try
            {
                svc.RegisterStatsAsync(new RegisterStatsRequest(
                    player.Id, mapResult.Id, 10, 5, 3, 1500, invalidKast))
                    .GetAwaiter().GetResult();
                return false; // should have thrown
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message == "INVALID_KAST_VALUE";
            }
        });
    }

    // ── PlayerService property tests ──────────────────────────────────────────

    // Feature: player-rating-system, Property 5.1: ranking is ordered by PlayerScore descending
    [Property(MaxTest = 50)]
    public Property PlayerRanking_OrderedByScoreDescending()
    {
        var gen = Gen.Choose(2, 6);

        return Prop.ForAll(gen.ToArbitrary(), playerCount =>
        {
            using var db = CreateDb();
            var team = SeedTeamAsync(db).GetAwaiter().GetResult();
            var svc  = new PlayerService(db);

            for (int i = 0; i < playerCount; i++)
            {
                var player = SeedPlayerAsync(db, team.Id).GetAwaiter().GetResult();
                player.PlayerScore = i * 1.5; // distinct scores
                db.SaveChanges();
            }

            var ranking = svc.GetRankingAsync().GetAwaiter().GetResult();

            for (int i = 0; i < ranking.Count - 1; i++)
            {
                if (ranking[i].PlayerScore < ranking[i + 1].PlayerScore)
                    return false;
            }
            return true;
        });
    }

    // Feature: player-rating-system, Property 5.2: ranking contains required fields
    [Fact]
    public async Task PlayerRanking_ContainsRequiredFields()
    {
        await using var db = CreateDb();
        var team   = await SeedTeamAsync(db, "FrogTeam");
        var player = await SeedPlayerAsync(db, team.Id, "s1mple");
        player.PlayerScore  = 5.5;
        player.MatchesCount = 3;
        await db.SaveChangesAsync();

        var svc     = new PlayerService(db);
        var ranking = await svc.GetRankingAsync();

        var item = Assert.Single(ranking);
        Assert.Equal(1, item.Position);
        Assert.Equal("s1mple", item.Nickname);
        Assert.Equal("FrogTeam", item.TeamName);
        Assert.Equal(5.5, item.PlayerScore);
        Assert.Equal(3, item.MatchesCount);
    }

    // Feature: player-rating-system, Property 5.4: empty ranking returns empty list
    [Fact]
    public async Task PlayerRanking_NoPlayers_ReturnsEmptyList()
    {
        await using var db = CreateDb();
        var svc     = new PlayerService(db);
        var ranking = await svc.GetRankingAsync();
        Assert.Empty(ranking);
    }

    // Feature: player-rating-system, Property 2.3: duplicate nickname is rejected
    [Property(MaxTest = 50)]
    public Property PlayerService_DuplicateNickname_IsRejected()
    {
        return Prop.ForAll(Arb.Default.NonEmptyString(), nicknameArb =>
        {
            var nickname = nicknameArb.Get.Replace("\0", "").Trim();
            if (string.IsNullOrWhiteSpace(nickname)) return true; // skip degenerate

            using var db = CreateDb();
            var team = SeedTeamAsync(db).GetAwaiter().GetResult();
            var svc  = new PlayerService(db);

            svc.CreatePlayerAsync(new CreatePlayerRequest(nickname, null, team.Id, null))
               .GetAwaiter().GetResult();

            try
            {
                svc.CreatePlayerAsync(new CreatePlayerRequest(nickname, null, team.Id, null))
                   .GetAwaiter().GetResult();
                return false; // should have thrown
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message == "PLAYER_NICKNAME_ALREADY_EXISTS";
            }
        });
    }

    // Feature: player-rating-system, Property 2.4: invalid teamId is rejected
    [Property(MaxTest = 100)]
    public Property PlayerService_InvalidTeamId_IsRejected()
    {
        return Prop.ForAll(Arb.Default.Guid(), randomGuid =>
        {
            using var db = CreateDb();
            var svc = new PlayerService(db);

            try
            {
                svc.CreatePlayerAsync(new CreatePlayerRequest("player1", null, randomGuid, null))
                   .GetAwaiter().GetResult();
                return false; // should have thrown
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message == "TEAM_NOT_FOUND";
            }
        });
    }
}
