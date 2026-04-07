using FrogBets.Api.Services;
using FrogBets.Domain.Entities;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace FrogBets.Tests;

public class AuditLogServiceTests
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

    private static AuditLogService CreateService(FrogBetsDbContext db)
        => new AuditLogService(db, NullLogger<AuditLogService>.Instance);

    private static AuditLogEntry MakeEntry(
        string? details = null,
        DateTime? occurredAt = null,
        Guid? actorId = null,
        string actorUsername = "user")
        => new AuditLogEntry(
            ActorId: actorId,
            ActorUsername: actorUsername,
            Action: "bets.create",
            ResourceType: "bet",
            ResourceId: null,
            HttpMethod: "POST",
            Route: "/api/bets",
            StatusCode: 201,
            IpAddress: "127.0.0.1",
            OccurredAt: occurredAt ?? DateTime.UtcNow,
            Details: details
        );

    // ── 1. Truncamento de Details > 1000 chars ────────────────────────────────

    [Fact]
    public async Task LogAsync_TruncatesDetailsOver1000Chars()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);
        var longDetails = new string('x', 1500);

        await svc.LogAsync(MakeEntry(details: longDetails));

        var log = await db.AuditLogs.SingleAsync();
        Assert.NotNull(log.Details);
        Assert.True(log.Details!.Length <= 1000);
    }

    // ── 2. Details < 1000 chars é preservado integralmente ───────────────────

    [Fact]
    public async Task LogAsync_PreservesDetailsUnder1000Chars()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);
        var shortDetails = new string('y', 500);

        await svc.LogAsync(MakeEntry(details: shortDetails));

        var log = await db.AuditLogs.SingleAsync();
        Assert.Equal(shortDetails, log.Details);
    }

    // ── 3. QueryAsync filtra por ActorId ─────────────────────────────────────

    [Fact]
    public async Task QueryAsync_FiltersBy_ActorId()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var targetId = Guid.NewGuid();
        var otherId1 = Guid.NewGuid();
        var otherId2 = Guid.NewGuid();

        await svc.LogAsync(MakeEntry(actorId: targetId, actorUsername: "target"));
        await svc.LogAsync(MakeEntry(actorId: targetId, actorUsername: "target"));
        await svc.LogAsync(MakeEntry(actorId: otherId1, actorUsername: "other1"));
        await svc.LogAsync(MakeEntry(actorId: otherId2, actorUsername: "other2"));

        var result = await svc.QueryAsync(new AuditLogQuery(
            ActorId: targetId,
            Action: null,
            From: null,
            To: null,
            Page: 1,
            PageSize: 50
        ));

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(targetId, item.ActorId));
    }

    // ── 4. QueryAsync filtra por From e To ───────────────────────────────────

    [Fact]
    public async Task QueryAsync_FiltersBy_FromAndTo()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var baseTime = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        await svc.LogAsync(MakeEntry(occurredAt: baseTime.AddDays(-2)));  // fora
        await svc.LogAsync(MakeEntry(occurredAt: baseTime));              // dentro (from)
        await svc.LogAsync(MakeEntry(occurredAt: baseTime.AddDays(1)));   // dentro
        await svc.LogAsync(MakeEntry(occurredAt: baseTime.AddDays(3)));   // fora (after to)

        var result = await svc.QueryAsync(new AuditLogQuery(
            ActorId: null,
            Action: null,
            From: baseTime,
            To: baseTime.AddDays(2),
            Page: 1,
            PageSize: 50
        ));

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item =>
        {
            Assert.True(item.OccurredAt >= baseTime);
            Assert.True(item.OccurredAt <= baseTime.AddDays(2));
        });
    }

    // ── 5. QueryAsync com From sem To retorna a partir de From ───────────────

    [Fact]
    public async Task QueryAsync_FiltersBy_From_WithoutTo()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var from = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        await svc.LogAsync(MakeEntry(occurredAt: from.AddDays(-1)));  // antes — excluído
        await svc.LogAsync(MakeEntry(occurredAt: from));              // exatamente from — incluído
        await svc.LogAsync(MakeEntry(occurredAt: from.AddDays(5)));   // depois — incluído
        await svc.LogAsync(MakeEntry(occurredAt: from.AddDays(30)));  // depois — incluído

        var result = await svc.QueryAsync(new AuditLogQuery(
            ActorId: null,
            Action: null,
            From: from,
            To: null,
            Page: 1,
            PageSize: 50
        ));

        Assert.Equal(3, result.TotalCount);
        Assert.All(result.Items, item => Assert.True(item.OccurredAt >= from));
    }

    // ── 6. QueryAsync limita pageSize a 100 ──────────────────────────────────

    [Fact]
    public async Task QueryAsync_LimitsPageSizeTo100()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        for (var i = 0; i < 150; i++)
            await svc.LogAsync(MakeEntry(occurredAt: DateTime.UtcNow.AddSeconds(i)));

        var result = await svc.QueryAsync(new AuditLogQuery(
            ActorId: null,
            Action: null,
            From: null,
            To: null,
            Page: 1,
            PageSize: 200
        ));

        Assert.Equal(150, result.TotalCount);
        Assert.True(result.Items.Count <= 100);
    }

    // ── 7. QueryAsync ordena por OccurredAt DESC ──────────────────────────────

    [Fact]
    public async Task QueryAsync_OrdersByOccurredAtDesc()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var base_ = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await svc.LogAsync(MakeEntry(occurredAt: base_.AddDays(2)));
        await svc.LogAsync(MakeEntry(occurredAt: base_));
        await svc.LogAsync(MakeEntry(occurredAt: base_.AddDays(1)));

        var result = await svc.QueryAsync(new AuditLogQuery(
            ActorId: null,
            Action: null,
            From: null,
            To: null,
            Page: 1,
            PageSize: 50
        ));

        var items = result.Items;
        for (var i = 0; i < items.Count - 1; i++)
            Assert.True(items[i].OccurredAt >= items[i + 1].OccurredAt);
    }

    // ── 8. DeleteExpiredAsync remove apenas logs expirados ───────────────────

    [Fact]
    public async Task DeleteExpiredAsync_RemovesOnlyExpiredLogs()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var cutoff = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        await svc.LogAsync(MakeEntry(occurredAt: cutoff.AddDays(-2)));  // expirado
        await svc.LogAsync(MakeEntry(occurredAt: cutoff.AddDays(-1)));  // expirado
        await svc.LogAsync(MakeEntry(occurredAt: cutoff));              // válido (>= cutoff)
        await svc.LogAsync(MakeEntry(occurredAt: cutoff.AddDays(1)));   // válido

        await svc.DeleteExpiredAsync(cutoff);

        var remaining = await db.AuditLogs.ToListAsync();
        Assert.Equal(2, remaining.Count);
        Assert.All(remaining, log => Assert.True(log.OccurredAt >= cutoff));
    }

    // ── 9. LogAsync não lança exceção quando o DbContext falha ───────────────

    [Fact]
    public async Task LogAsync_DoesNotThrow_WhenDbFails()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        // Descartar o contexto para simular falha de banco
        await db.DisposeAsync();

        var exception = await Record.ExceptionAsync(() =>
            svc.LogAsync(MakeEntry()));

        Assert.Null(exception);
    }
}
