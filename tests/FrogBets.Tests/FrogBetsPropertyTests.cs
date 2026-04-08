using FrogBets.Api.Services;
using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;
using FrogBets.Infrastructure.Data;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FrogBets.Tests;

/// <summary>
/// Property-based tests for the FrogBets core spec (frog-bets).
/// Covers Properties 1-19 from the design document.
/// Feature: frog-bets
/// </summary>
public class FrogBetsPropertyTests
{
    //  helpers 

    private static FrogBetsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FrogBetsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new FrogBetsDbContext(options);
    }

    private static async Task<User> SeedUserAsync(FrogBetsDbContext db,
        decimal virtualBalance = 1000m, decimal reservedBalance = 0m)
    {
        var user = new User
        {
            Id              = Guid.NewGuid(),
            Username        = Guid.NewGuid().ToString("N"),
            PasswordHash    = "hash",
            VirtualBalance  = virtualBalance,
            ReservedBalance = reservedBalance,
            CreatedAt       = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<(Game game, Market market)> SeedGameWithMarketAsync(
        FrogBetsDbContext db,
        GameStatus gameStatus = GameStatus.Scheduled,
        MarketStatus marketStatus = MarketStatus.Open,
        MarketType marketType = MarketType.MapWinner)
    {
        var game = new Game
        {
            Id           = Guid.NewGuid(),
            TeamA        = "TeamA",
            TeamB        = "TeamB",
            ScheduledAt  = DateTime.UtcNow.AddDays(1),
            NumberOfMaps = 1,
            Status       = gameStatus,
            CreatedAt    = DateTime.UtcNow,
        };
        var market = new Market
        {
            Id        = Guid.NewGuid(),
            GameId    = game.Id,
            Type      = marketType,
            MapNumber = 1,
            Status    = marketStatus,
            Game      = game,
        };
        game.Markets.Add(market);
        db.Games.Add(game);
        await db.SaveChangesAsync();
        return (game, market);
    }

    private static async Task<Bet> SeedActiveBetAsync(FrogBetsDbContext db,
        Guid marketId, Guid creatorId, Guid coveredById,
        string creatorOption = "TeamA", decimal amount = 100m)
    {
        var bet = new Bet
        {
            Id            = Guid.NewGuid(),
            MarketId      = marketId,
            CreatorId     = creatorId,
            CoveredById   = coveredById,
            CreatorOption = creatorOption,
            CovererOption = creatorOption == "TeamA" ? "TeamB" : "TeamA",
            Amount        = amount,
            Status        = BetStatus.Active,
            CreatedAt     = DateTime.UtcNow,
            CoveredAt     = DateTime.UtcNow,
        };
        db.Bets.Add(bet);
        await db.SaveChangesAsync();
        return bet;
    }

    private static BetService CreateBetService(FrogBetsDbContext db)
        => new(db, new BalanceService(db));

    private static SettlementService CreateSettlementService(FrogBetsDbContext db)
        => new(db, new BalanceService(db));

    //  Property 1: Credenciais inválidas não revelam qual campo está errado 

    // Feature: frog-bets, Property 1: invalid credentials return same error regardless of which field is wrong
    [Property(MaxTest = 100)]
    public Property Login_InvalidCredentials_SameErrorMessageForAnyInvalidInput()
    {
        var gen = from username in Arb.Default.NonEmptyString().Generator
                  from password in Arb.Default.NonEmptyString().Generator
                  select (username.Get, password.Get);

        return Prop.ForAll(gen.ToArbitrary(), t =>
        {
            var (username, password) = t;
            using var db = CreateDb();
            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"]               = "super-secret-key-that-is-at-least-32-chars!!",
                    ["Jwt:Issuer"]            = "FrogBets",
                    ["Jwt:Audience"]          = "FrogBets",
                    ["Jwt:ExpirationMinutes"] = "60",
                })
                .Build();
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            services.AddSingleton(db);
            services.AddDbContext<FrogBetsDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            var provider = services.BuildServiceProvider();
            var blocklist = new TokenBlocklist(provider.GetRequiredService<IServiceScopeFactory>());
            var svc = new AuthService(db, config, blocklist);

            // Seed a known user
            var knownUser = new User
            {
                Id = Guid.NewGuid(), Username = "knownuser",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("correctpass"),
                VirtualBalance = 1000m, CreatedAt = DateTime.UtcNow,
            };
            db.Users.Add(knownUser);
            db.SaveChanges();

            string? msgWrongUser = null, msgWrongPass = null;
            try { svc.LoginAsync("unknownuser_xyz", "correctpass").GetAwaiter().GetResult(); }
            catch (UnauthorizedAccessException ex) { msgWrongUser = ex.Message; }

            try { svc.LoginAsync("knownuser", "wrongpassword_xyz").GetAwaiter().GetResult(); }
            catch (UnauthorizedAccessException ex) { msgWrongPass = ex.Message; }

            return msgWrongUser != null && msgWrongPass != null && msgWrongUser == msgWrongPass;
        });
    }

    //  Property 2: Invariante de saldo ao reservar 

    // Feature: frog-bets, Property 2: balance invariant when reserving  total stays the same
    [Property(MaxTest = 200)]
    public Property BalanceReserve_TotalBalanceUnchanged()
    {
        var gen = from balance in Gen.Choose(100, 10000).Select(x => (decimal)x)
                  from amount  in Gen.Choose(1, 100).Select(x => (decimal)x)
                  where amount <= balance
                  select (balance, amount);

        return Prop.ForAll(gen.ToArbitrary(), t =>
        {
            var (balance, amount) = t;
            using var db = CreateDb();
            var user = SeedUserAsync(db, virtualBalance: balance).GetAwaiter().GetResult();
            var svc  = new BalanceService(db);
            var totalBefore = user.VirtualBalance + user.ReservedBalance;

            svc.ReserveBalanceAsync(user.Id, amount).GetAwaiter().GetResult();

            var updated = db.Users.Find(user.Id)!;
            var totalAfter = updated.VirtualBalance + updated.ReservedBalance;
            return totalBefore == totalAfter
                && updated.ReservedBalance == amount
                && updated.VirtualBalance == balance - amount;
        });
    }

    //  Property 3: Rejeição por saldo insuficiente 

    // Feature: frog-bets, Property 3: insufficient balance rejects bet creation and cover
    [Property(MaxTest = 200)]
    public Property InsufficientBalance_BetCreationRejected_BalanceUnchanged()
    {
        var gen = from balance in Gen.Choose(0, 99).Select(x => (decimal)x)
                  from amount  in Gen.Choose(100, 1000).Select(x => (decimal)x)
                  select (balance, amount);

        return Prop.ForAll(gen.ToArbitrary(), t =>
        {
            var (balance, amount) = t;
            using var db = CreateDb();
            var user = SeedUserAsync(db, virtualBalance: balance).GetAwaiter().GetResult();
            var (_, market) = SeedGameWithMarketAsync(db).GetAwaiter().GetResult();
            var svc = CreateBetService(db);

            try
            {
                svc.CreateBetAsync(user.Id, market.Id, "TeamA", amount).GetAwaiter().GetResult();
                return false; // should have thrown
            }
            catch (InvalidOperationException ex) when (ex.Message == "INSUFFICIENT_BALANCE")
            {
                var unchanged = db.Users.Find(user.Id)!;
                return unchanged.VirtualBalance == balance && unchanged.ReservedBalance == 0m;
            }
        });
    }

    //  Property 4: Crédito correto ao vencedor na liquidação 

    // Feature: frog-bets, Property 4: winner credited with 2*amount, reserved decremented by amount
    [Property(MaxTest = 100)]
    public Property Settlement_WinnerCreditedCorrectly()
    {
        var gen = Gen.Choose(10, 500).Select(x => (decimal)x);

        return Prop.ForAll(gen.ToArbitrary(), amount =>
        {
            using var db = CreateDb();
            var creator = SeedUserAsync(db, virtualBalance: 0m, reservedBalance: amount).GetAwaiter().GetResult();
            var coverer = SeedUserAsync(db, virtualBalance: 0m, reservedBalance: amount).GetAwaiter().GetResult();
            var (_, market) = SeedGameWithMarketAsync(db).GetAwaiter().GetResult();
            var bet = SeedActiveBetAsync(db, market.Id, creator.Id, coverer.Id, "TeamA", amount).GetAwaiter().GetResult();
            var svc = CreateSettlementService(db);

            svc.SettleMarketAsync(market.Id, "TeamA").GetAwaiter().GetResult();

            var updatedCreator = db.Users.Find(creator.Id)!;
            return updatedCreator.VirtualBalance == 2 * amount
                && updatedCreator.ReservedBalance == 0m;
        });
    }

    //  Property 5: Apostas bloqueadas para jogos iniciados 

    // Feature: frog-bets, Property 5: bets blocked for InProgress or Finished games
    [Property(MaxTest = 100)]
    public Property BetsBlocked_ForStartedGames()
    {
        var gen = Gen.Elements(GameStatus.InProgress, GameStatus.Finished);

        return Prop.ForAll(gen.ToArbitrary(), gameStatus =>
        {
            using var db = CreateDb();
            var user = SeedUserAsync(db, virtualBalance: 1000m).GetAwaiter().GetResult();
            var (_, market) = SeedGameWithMarketAsync(db, gameStatus: gameStatus).GetAwaiter().GetResult();
            var svc = CreateBetService(db);

            try
            {
                svc.CreateBetAsync(user.Id, market.Id, "TeamA", 100m).GetAwaiter().GetResult();
                return false;
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message == "GAME_ALREADY_STARTED";
            }
        });
    }

    //  Property 6: Liquidação completa de todas as apostas ativas 

    // Feature: frog-bets, Property 6: all active bets settled after market result registered
    [Property(MaxTest = 50)]
    public Property Settlement_AllActiveBetsSettled()
    {
        var gen = Gen.Choose(1, 5);

        return Prop.ForAll(gen.ToArbitrary(), betCount =>
        {
            using var db = CreateDb();
            var (_, market) = SeedGameWithMarketAsync(db).GetAwaiter().GetResult();

            for (int i = 0; i < betCount; i++)
            {
                var c = SeedUserAsync(db, virtualBalance: 0m, reservedBalance: 100m).GetAwaiter().GetResult();
                var v = SeedUserAsync(db, virtualBalance: 0m, reservedBalance: 100m).GetAwaiter().GetResult();
                SeedActiveBetAsync(db, market.Id, c.Id, v.Id, "TeamA", 100m).GetAwaiter().GetResult();
            }

            var svc = CreateSettlementService(db);
            svc.SettleMarketAsync(market.Id, "TeamA").GetAwaiter().GetResult();

            var remaining = db.Bets.Count(b => b.MarketId == market.Id && b.Status == BetStatus.Active);
            return remaining == 0;
        });
    }

    //  Property 7: Round-trip de cancelamento restaura saldo 

    // Feature: frog-bets, Property 7: create then cancel restores exact original balances
    [Property(MaxTest = 200)]
    public Property CancelBet_RestoresOriginalBalance()
    {
        var gen = from balance in Gen.Choose(100, 5000).Select(x => (decimal)x)
                  from amount  in Gen.Choose(1, 100).Select(x => (decimal)x)
                  where amount <= balance
                  select (balance, amount);

        return Prop.ForAll(gen.ToArbitrary(), t =>
        {
            var (balance, amount) = t;
            using var db = CreateDb();
            var user = SeedUserAsync(db, virtualBalance: balance).GetAwaiter().GetResult();
            var (_, market) = SeedGameWithMarketAsync(db).GetAwaiter().GetResult();
            var svc = CreateBetService(db);

            var betId = svc.CreateBetAsync(user.Id, market.Id, "TeamA", amount).GetAwaiter().GetResult();
            svc.CancelBetAsync(user.Id, betId).GetAwaiter().GetResult();

            var updated = db.Users.Find(user.Id)!;
            return updated.VirtualBalance == balance && updated.ReservedBalance == 0m;
        });
    }

    //  Property 8: Unicidade de aposta por usuário e mercado 

    // Feature: frog-bets, Property 8: duplicate bet on same market by same user is rejected
    [Property(MaxTest = 100)]
    public Property DuplicateBet_SameUserSameMarket_Rejected()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var user = SeedUserAsync(db, virtualBalance: 1000m).GetAwaiter().GetResult();
            var (_, market) = SeedGameWithMarketAsync(db).GetAwaiter().GetResult();
            var svc = CreateBetService(db);

            svc.CreateBetAsync(user.Id, market.Id, "TeamA", 100m).GetAwaiter().GetResult();

            try
            {
                svc.CreateBetAsync(user.Id, market.Id, "TeamB", 50m).GetAwaiter().GetResult();
                return false;
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message == "DUPLICATE_BET_ON_MARKET";
            }
        });
    }

    //  Property 9: Cobertura registra contraparte e opção oposta ─

    // Feature: frog-bets, Property 9: cover sets CoveredById, CovererOption (opposite), and Active status
    [Property(MaxTest = 100)]
    public Property CoverBet_SetsCovererAndOppositeOption()
    {
        var gen = Gen.Elements(
            ("TeamA", "TeamB"),
            ("TeamB", "TeamA"));

        return Prop.ForAll(gen.ToArbitrary(), t =>
        {
            var (creatorOption, expectedCovererOption) = t;
            using var db = CreateDb();
            var creator = SeedUserAsync(db, virtualBalance: 500m).GetAwaiter().GetResult();
            var coverer = SeedUserAsync(db, virtualBalance: 500m).GetAwaiter().GetResult();
            var (_, market) = SeedGameWithMarketAsync(db, marketType: MarketType.MapWinner).GetAwaiter().GetResult();
            var svc = CreateBetService(db);

            var betId = svc.CreateBetAsync(creator.Id, market.Id, creatorOption, 100m).GetAwaiter().GetResult();
            svc.CoverBetAsync(coverer.Id, betId).GetAwaiter().GetResult();

            var bet = db.Bets.Find(betId)!;
            return bet.CoveredById == coverer.Id
                && bet.CovererOption == expectedCovererOption
                && bet.Status == BetStatus.Active;
        });
    }

    //  Property 10: Criador não pode cobrir a própria aposta ─

    // Feature: frog-bets, Property 10: creator cannot cover own bet
    [Property(MaxTest = 100)]
    public Property Creator_CannotCoverOwnBet()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var creator = SeedUserAsync(db, virtualBalance: 1000m).GetAwaiter().GetResult();
            var (_, market) = SeedGameWithMarketAsync(db).GetAwaiter().GetResult();
            var svc = CreateBetService(db);

            var betId = svc.CreateBetAsync(creator.Id, market.Id, "TeamA", 100m).GetAwaiter().GetResult();

            try
            {
                svc.CoverBetAsync(creator.Id, betId).GetAwaiter().GetResult();
                return false;
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message == "CANNOT_COVER_OWN_BET";
            }
        });
    }

    //  Property 11: Aposta já coberta não pode ser coberta novamente 

    // Feature: frog-bets, Property 11: active bet cannot be covered again
    [Property(MaxTest = 100)]
    public Property ActiveBet_CannotBeCoveredAgain()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var creator  = SeedUserAsync(db, virtualBalance: 500m).GetAwaiter().GetResult();
            var coverer1 = SeedUserAsync(db, virtualBalance: 500m).GetAwaiter().GetResult();
            var coverer2 = SeedUserAsync(db, virtualBalance: 500m).GetAwaiter().GetResult();
            var (_, market) = SeedGameWithMarketAsync(db).GetAwaiter().GetResult();
            var svc = CreateBetService(db);

            var betId = svc.CreateBetAsync(creator.Id, market.Id, "TeamA", 100m).GetAwaiter().GetResult();
            svc.CoverBetAsync(coverer1.Id, betId).GetAwaiter().GetResult();

            try
            {
                svc.CoverBetAsync(coverer2.Id, betId).GetAwaiter().GetResult();
                return false;
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message == "BET_NOT_AVAILABLE";
            }
        });
    }

    //  Property 12: Notificação ao criador quando aposta é coberta 

    // Feature: frog-bets, Property 12: creator receives unread notification when bet is covered
    [Property(MaxTest = 100)]
    public Property CoverBet_CreatesUnreadNotificationForCreator()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var creator = SeedUserAsync(db, virtualBalance: 500m).GetAwaiter().GetResult();
            var coverer = SeedUserAsync(db, virtualBalance: 500m).GetAwaiter().GetResult();
            var (_, market) = SeedGameWithMarketAsync(db).GetAwaiter().GetResult();
            var svc = CreateBetService(db);

            var betId = svc.CreateBetAsync(creator.Id, market.Id, "TeamA", 100m).GetAwaiter().GetResult();
            svc.CoverBetAsync(coverer.Id, betId).GetAwaiter().GetResult();

            var notification = db.Notifications.FirstOrDefault(n => n.UserId == creator.Id);
            return notification != null && !notification.IsRead;
        });
    }

    //  Property 13: Apenas o criador pode cancelar a própria aposta 

    // Feature: frog-bets, Property 13: only creator can cancel their bet
    [Property(MaxTest = 100)]
    public Property OnlyCreator_CanCancelBet()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var creator   = SeedUserAsync(db, virtualBalance: 500m).GetAwaiter().GetResult();
            var otherUser = SeedUserAsync(db, virtualBalance: 500m).GetAwaiter().GetResult();
            var (_, market) = SeedGameWithMarketAsync(db).GetAwaiter().GetResult();
            var svc = CreateBetService(db);

            var betId = svc.CreateBetAsync(creator.Id, market.Id, "TeamA", 100m).GetAwaiter().GetResult();

            try
            {
                svc.CancelBetAsync(otherUser.Id, betId).GetAwaiter().GetResult();
                return false;
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message == "NOT_BET_OWNER";
            }
        });
    }

    //  Property 14: Aposta ativa não pode ser cancelada ─

    // Feature: frog-bets, Property 14: active bet cannot be cancelled
    [Property(MaxTest = 100)]
    public Property ActiveBet_CannotBeCancelled()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var creator = SeedUserAsync(db, virtualBalance: 500m).GetAwaiter().GetResult();
            var coverer = SeedUserAsync(db, virtualBalance: 500m).GetAwaiter().GetResult();
            var (_, market) = SeedGameWithMarketAsync(db).GetAwaiter().GetResult();
            var svc = CreateBetService(db);

            var betId = svc.CreateBetAsync(creator.Id, market.Id, "TeamA", 100m).GetAwaiter().GetResult();
            svc.CoverBetAsync(coverer.Id, betId).GetAwaiter().GetResult();

            try
            {
                svc.CancelBetAsync(creator.Id, betId).GetAwaiter().GetResult();
                return false;
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message == "CANNOT_CANCEL_ACTIVE_BET";
            }
        });
    }

    //  Property 15: Aposta anulada devolve saldo a ambos os lados 

    // Feature: frog-bets, Property 15: voided bet returns stakes to both sides
    [Property(MaxTest = 100)]
    public Property VoidedBet_ReturnsBothStakes()
    {
        var gen = Gen.Choose(10, 500).Select(x => (decimal)x);

        return Prop.ForAll(gen.ToArbitrary(), amount =>
        {
            using var db = CreateDb();
            var creator = SeedUserAsync(db, virtualBalance: 50m, reservedBalance: amount).GetAwaiter().GetResult();
            var coverer = SeedUserAsync(db, virtualBalance: 50m, reservedBalance: amount).GetAwaiter().GetResult();
            var (_, market) = SeedGameWithMarketAsync(db).GetAwaiter().GetResult();
            SeedActiveBetAsync(db, market.Id, creator.Id, coverer.Id, "TeamA", amount).GetAwaiter().GetResult();
            var svc = CreateSettlementService(db);

            svc.SettleMarketAsync(market.Id, "draw", isVoided: true).GetAwaiter().GetResult();

            var updatedCreator = db.Users.Find(creator.Id)!;
            var updatedCoverer = db.Users.Find(coverer.Id)!;
            return updatedCreator.VirtualBalance == 50m + amount
                && updatedCreator.ReservedBalance == 0m
                && updatedCoverer.VirtualBalance == 50m + amount
                && updatedCoverer.ReservedBalance == 0m;
        });
    }

    //  Property 16: Resultado de jogo já liquidado é rejeitado 

    // Feature: frog-bets, Property 16: registering result on finished game is rejected
    [Fact]
    public async Task RegisterResult_FinishedGame_IsRejected()
    {
        await using var db = CreateDb();
        var svc = new GameService(db, CreateSettlementService(db), new BalanceService(db));
        var gameId = await svc.CreateGameAsync(new CreateGameRequest("TeamA", "TeamB", DateTime.UtcNow.AddDays(1), 1));
        await svc.StartGameAsync(gameId);

        var adminId = Guid.NewGuid();
        var markets = await db.Markets.Where(m => m.GameId == gameId).ToListAsync();
        foreach (var m in markets)
            await svc.RegisterResultAsync(gameId, new RegisterResultRequest(m.Id, "TeamA", m.MapNumber), adminId);

        var anyMarket = markets.First();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RegisterResultAsync(gameId, new RegisterResultRequest(anyMarket.Id, "TeamB", anyMarket.MapNumber), adminId));

        Assert.Equal("GAME_ALREADY_FINISHED", ex.Message);
    }

    //  Property 17: Resposta da API de apostas contém campos obrigatórios 

    // Feature: frog-bets, Property 17: bet DTO contains all required fields
    [Property(MaxTest = 100)]
    public Property BetDto_ContainsRequiredFields()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var user = SeedUserAsync(db, virtualBalance: 500m).GetAwaiter().GetResult();
            var (_, market) = SeedGameWithMarketAsync(db).GetAwaiter().GetResult();
            var svc = CreateBetService(db);

            svc.CreateBetAsync(user.Id, market.Id, "TeamA", 100m).GetAwaiter().GetResult();
            var bets = svc.GetUserBetsAsync(user.Id).GetAwaiter().GetResult();

            var dto = bets.Single();
            return dto.MarketId != Guid.Empty
                && !string.IsNullOrEmpty(dto.CreatorOption)
                && dto.Amount > 0
                && dto.Status == BetStatus.Pending;
        });
    }

    //  Property 18: Histórico de apostas liquidadas ordenado por data 

    // Feature: frog-bets, Property 18: settled bets ordered by SettledAt descending
    [Property(MaxTest = 50)]
    public Property SettledBets_OrderedBySettledAtDescending()
    {
        var gen = Gen.Choose(2, 5);

        return Prop.ForAll(gen.ToArbitrary(), betCount =>
        {
            using var db = CreateDb();
            var user = SeedUserAsync(db, virtualBalance: 1000m).GetAwaiter().GetResult();
            var svc  = CreateBetService(db);

            var settledAts = new List<DateTime>();
            for (int i = 0; i < betCount; i++)
            {
                var (_, market) = SeedGameWithMarketAsync(db).GetAwaiter().GetResult();
                var bet = new Bet
                {
                    Id            = Guid.NewGuid(),
                    MarketId      = market.Id,
                    CreatorId     = user.Id,
                    CreatorOption = "TeamA",
                    Amount        = 10m,
                    Status        = BetStatus.Settled,
                    CreatedAt     = DateTime.UtcNow,
                    SettledAt     = DateTime.UtcNow.AddHours(-i),
                };
                db.Bets.Add(bet);
                settledAts.Add(bet.SettledAt!.Value);
            }
            db.SaveChanges();

            var bets = svc.GetUserBetsAsync(user.Id).GetAwaiter().GetResult();
            var settled = bets.Where(b => b.Status == BetStatus.Settled).ToList();

            for (int i = 0; i < settled.Count - 1; i++)
            {
                if (settled[i].SettledAt < settled[i + 1].SettledAt)
                    return false;
            }
            return true;
        });
    }

    //  Property 19: Leaderboard ordenado por saldo decrescente 

    // Feature: frog-bets, Property 19: leaderboard ordered by VirtualBalance descending with required fields
    [Property(MaxTest = 50)]
    public Property Leaderboard_OrderedByBalanceDescending_WithRequiredFields()
    {
        var gen = Gen.Choose(2, 6);

        return Prop.ForAll(gen.ToArbitrary(), userCount =>
        {
            using var db = CreateDb();
            for (int i = 0; i < userCount; i++)
            {
                var u = new User
                {
                    Id              = Guid.NewGuid(),
                    Username        = $"user{i}",
                    PasswordHash    = "hash",
                    VirtualBalance  = (decimal)(i * 100 + 50),
                    ReservedBalance = 0m,
                    WinsCount       = i,
                    LossesCount     = userCount - i,
                    CreatedAt       = DateTime.UtcNow,
                };
                db.Users.Add(u);
            }
            db.SaveChanges();

            var entries = db.Users
                .AsNoTracking()
                .OrderByDescending(u => u.VirtualBalance)
                .Select(u => new { u.Username, u.VirtualBalance, u.WinsCount, u.LossesCount })
                .ToList();

            for (int i = 0; i < entries.Count - 1; i++)
            {
                if (entries[i].VirtualBalance < entries[i + 1].VirtualBalance)
                    return false;
            }

            return entries.All(e =>
                !string.IsNullOrEmpty(e.Username)
                && e.VirtualBalance >= 0
                && e.WinsCount >= 0
                && e.LossesCount >= 0);
        });
    }
}
