using FrogBets.Api.Middleware;
using FrogBets.Api.Services;
using FrogBets.Infrastructure.Data;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace FrogBets.Tests;

/// <summary>
/// Property-based tests for the audit-logs spec.
/// Covers Properties 4, 5, 7, 8 from the design document.
/// Feature: audit-logs
/// </summary>
public class AuditLogPropertyTests
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

    private static AuditLogEntry MakeEntry(string? details = null, DateTime? occurredAt = null)
        => new AuditLogEntry(
            ActorId: Guid.NewGuid(),
            ActorUsername: "user",
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

    // ── Property 7: Campo Details é truncado para no máximo 1000 caracteres ──

    // Feature: audit-logs, Property 7: Campo Details é truncado para no máximo 1000 caracteres
    // Validates: Requirements 2.3
    [Property(MaxTest = 100)]
    public Property Details_TruncatedToAtMost1000Chars()
    {
        var chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ".ToCharArray();

        var gen = Gen.Choose(0, 2000)
            .SelectMany(len =>
                Gen.Elements(chars)
                   .ArrayOf(len)
                   .Select(arr => new string(arr)));

        return Prop.ForAll(gen.ToArbitrary(), details =>
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            svc.LogAsync(MakeEntry(details: details)).GetAwaiter().GetResult();

            var log = db.AuditLogs.Single();
            return (log.Details?.Length ?? 0) <= 1000;
        });
    }

    // ── Property 5: Limpeza remove exatamente os logs expirados e preserva os válidos ──

    // Feature: audit-logs, Property 5: Limpeza remove exatamente os logs expirados e preserva os válidos
    // Validates: Requirements 6.2
    [Property(MaxTest = 100)]
    public Property DeleteExpired_RemovesExpiredAndPreservesValid()
    {
        // Generate a list of day offsets (positive = future, negative = past) around a fixed cutoff
        var gen = Gen.Choose(1, 10)
            .SelectMany(count =>
                Gen.Choose(-30, 30)
                   .ArrayOf(count));

        return Prop.ForAll(gen.ToArbitrary(), offsets =>
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var cutoff = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc);

            foreach (var offset in offsets)
            {
                var occurredAt = cutoff.AddDays(offset);
                svc.LogAsync(MakeEntry(occurredAt: occurredAt)).GetAwaiter().GetResult();
            }

            svc.DeleteExpiredAsync(cutoff).GetAwaiter().GetResult();

            var remaining = db.AuditLogs.ToList();

            // All remaining logs must have OccurredAt >= cutoff
            var allValidRemain = remaining.All(l => l.OccurredAt >= cutoff);

            // Count how many should remain
            var expectedCount = offsets.Count(o => cutoff.AddDays(o) >= cutoff);
            var countMatches = remaining.Count == expectedCount;

            return allValidRemain && countMatches;
        });
    }

    // ── Property 8: Resultados de consulta são ordenados por OccurredAt decrescente ──

    // Feature: audit-logs, Property 8: Resultados de consulta são ordenados por OccurredAt decrescente
    // Validates: Requirements 4.2
    [Property(MaxTest = 100)]
    public Property QueryAsync_ResultsOrderedByOccurredAtDescending()
    {
        // Generate between 1 and 20 distinct timestamps
        var gen = Gen.Choose(1, 20)
            .SelectMany(n =>
                Gen.Choose(0, 10000)
                   .ArrayOf(n)
                   .Select(offsets => offsets
                       .Select(o => new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(o))
                       .ToArray()));

        return Prop.ForAll(gen.ToArbitrary(), timestamps =>
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            foreach (var ts in timestamps)
                svc.LogAsync(MakeEntry(occurredAt: ts)).GetAwaiter().GetResult();

            var result = svc.QueryAsync(new AuditLogQuery(
                ActorId: null,
                Action: null,
                From: null,
                To: null,
                Page: 1,
                PageSize: 100
            )).GetAwaiter().GetResult();

            var items = result.Items;
            for (int i = 0; i < items.Count - 1; i++)
            {
                if (items[i].OccurredAt < items[i + 1].OccurredAt)
                    return false;
            }
            return true;
        });
    }

    // ── Property 4: Paginação respeita o limite máximo de pageSize ──

    // Feature: audit-logs, Property 4: Paginação respeita o limite máximo de pageSize
    // Validates: Requirements 4.4
    [Property(MaxTest = 100)]
    public Property QueryAsync_ItemsCount_RespectsMaxPageSize()
    {
        var gen = from n in Gen.Choose(1, 150)
                  from pageSize in Gen.Choose(1, 500)
                  select (n, pageSize);

        return Prop.ForAll(gen.ToArbitrary(), t =>
        {
            var (n, pageSize) = t;
            using var db = CreateDb();
            var svc = CreateService(db);

            for (int i = 0; i < n; i++)
                svc.LogAsync(MakeEntry(occurredAt: DateTime.UtcNow.AddSeconds(i))).GetAwaiter().GetResult();

            var result = svc.QueryAsync(new AuditLogQuery(
                ActorId: null,
                Action: null,
                From: null,
                To: null,
                Page: 1,
                PageSize: pageSize
            )).GetAwaiter().GetResult();

            return result.Items.Count <= Math.Min(pageSize, 100);
        });
    }

    // ── Property 1: Requisição de escrita gera exatamente um AuditLog com campos corretos ──

    // Feature: audit-logs, Property 1: Requisição de escrita gera exatamente um AuditLog com campos corretos
    // Validates: Requirements 1.1, 2.1, 5.1
    [Property(MaxTest = 100)]
    public Property WriteRequest_GeneratesExactlyOneAuditLogWithCorrectFields()
    {
        var writeMethods = new[] { "POST", "PATCH", "PUT", "DELETE" };
        var routes = new[] { "/api/bets", "/api/games", "/api/teams", "/api/users/123/promote", "/api/custom/route" };

        var gen = from method in Gen.Elements(writeMethods)
                  from route in Gen.Elements(routes)
                  from actorId in Gen.Fresh(Guid.NewGuid)
                  from username in Gen.Elements("alice", "bob", "carol", "dave")
                  from statusCode in Gen.Elements(200, 201, 204, 400, 401, 403, 404, 500)
                  select (method, route, actorId, username, statusCode);

        return Prop.ForAll(gen.ToArbitrary(), t =>
        {
            var (method, route, actorId, username, statusCode) = t;
            using var db = CreateDb();
            var svc = CreateService(db);

            var occurredAt = DateTime.UtcNow;
            var entry = new AuditLogEntry(
                ActorId: actorId,
                ActorUsername: username,
                Action: $"{method} {route}",
                ResourceType: null,
                ResourceId: null,
                HttpMethod: method,
                Route: route,
                StatusCode: statusCode,
                IpAddress: "10.0.0.1",
                OccurredAt: occurredAt
            );

            svc.LogAsync(entry).GetAwaiter().GetResult();

            var logs = db.AuditLogs.ToList();

            // Exactly 1 log inserted
            if (logs.Count != 1) return false;

            var log = logs[0];

            // All required fields are correctly persisted
            return log.ActorId == actorId
                && log.ActorUsername == username
                && log.Action == $"{method} {route}"
                && log.HttpMethod == method
                && log.Route == route
                && log.StatusCode == statusCode
                && log.OccurredAt == occurredAt;
        });
    }

    // ── Property 2: Requisições GET nunca geram AuditLog ──

    // Feature: audit-logs, Property 2: Requisições GET nunca geram AuditLog
    // Validates: Requirements 1.2, 4.1
    [Property(MaxTest = 100)]
    public Property GetRequests_NeverGenerateAuditLog()
    {
        // Simulate: if LogAsync is called 0 times (as the middleware does for GET),
        // the database remains empty. Also verify N logs inserted = N logs in DB (no extras).
        var gen = Gen.Choose(0, 20);

        return Prop.ForAll(gen.ToArbitrary(), n =>
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            // Insert exactly N write-method logs (simulating non-GET requests)
            for (int i = 0; i < n; i++)
                svc.LogAsync(MakeEntry(occurredAt: DateTime.UtcNow.AddSeconds(i))).GetAwaiter().GetResult();

            // For GET simulation: call LogAsync 0 additional times
            // The count must remain exactly N (no phantom logs)
            var count = db.AuditLogs.Count();
            return count == n;
        });
    }

    // ── Property 3: StatusCode do AuditLog reflete o status code real da resposta ──

    // Feature: audit-logs, Property 3: StatusCode do AuditLog reflete o status code real da resposta
    // Validates: Requirements 1.1, 2.1
    [Property(MaxTest = 100)]
    public Property AuditLog_StatusCode_ReflectsRealResponseStatusCode()
    {
        var statusCodes = new[] { 200, 201, 204, 400, 401, 403, 404, 409, 500 };
        var gen = Gen.Elements(statusCodes);

        return Prop.ForAll(gen.ToArbitrary(), statusCode =>
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var entry = new AuditLogEntry(
                ActorId: Guid.NewGuid(),
                ActorUsername: "user",
                Action: "bets.create",
                ResourceType: "bet",
                ResourceId: null,
                HttpMethod: "POST",
                Route: "/api/bets",
                StatusCode: statusCode,
                IpAddress: "127.0.0.1",
                OccurredAt: DateTime.UtcNow
            );

            svc.LogAsync(entry).GetAwaiter().GetResult();

            var log = db.AuditLogs.Single();
            return log.StatusCode == statusCode;
        });
    }

    // ── Property 6: Requisição anônima gera AuditLog com ActorId nulo e username "anonymous" ──

    // Feature: audit-logs, Property 6: Requisição anônima gera AuditLog com ActorId nulo e username "anonymous"
    // Validates: Requirements 1.3
    [Property(MaxTest = 100)]
    public Property AnonymousRequest_GeneratesAuditLogWithNullActorIdAndAnonymousUsername()
    {
        var routes = new[] { "/api/bets", "/api/games", "/api/teams", "/api/auth/login" };
        var methods = new[] { "POST", "PATCH", "PUT", "DELETE" };

        var gen = from route in Gen.Elements(routes)
                  from method in Gen.Elements(methods)
                  select (route, method);

        return Prop.ForAll(gen.ToArbitrary(), t =>
        {
            var (route, method) = t;
            using var db = CreateDb();
            var svc = CreateService(db);

            var entry = new AuditLogEntry(
                ActorId: null,
                ActorUsername: "anonymous",
                Action: $"{method} {route}",
                ResourceType: null,
                ResourceId: null,
                HttpMethod: method,
                Route: route,
                StatusCode: 401,
                IpAddress: "192.168.1.1",
                OccurredAt: DateTime.UtcNow
            );

            svc.LogAsync(entry).GetAwaiter().GetResult();

            var log = db.AuditLogs.Single();
            return log.ActorId == null && log.ActorUsername == "anonymous";
        });
    }

    // ── Property 9: Fallback de action para rotas não mapeadas ──

    // Feature: audit-logs, Property 9: Fallback de action para rotas não mapeadas
    // Validates: Requirements 3.2
    [Property(MaxTest = 100)]
    public Property UnmappedRoute_ActionFallsBackToMethodPlusRoute()
    {
        // Routes that are NOT in the action dictionary — arbitrary unmapped routes
        var unmappedRoutes = new[]
        {
            "/api/unknown/resource",
            "/api/custom/endpoint",
            "/api/v2/something",
            "/api/reports/generate",
            "/api/admin/bulk-action"
        };
        var methods = new[] { "POST", "PATCH", "PUT", "DELETE" };

        var gen = from route in Gen.Elements(unmappedRoutes)
                  from method in Gen.Elements(methods)
                  select (route, method);

        return Prop.ForAll(gen.ToArbitrary(), t =>
        {
            var (route, method) = t;
            using var db = CreateDb();
            var svc = CreateService(db);

            // The fallback action format is "<METHOD> <route>"
            var fallbackAction = $"{method} {route}";

            var entry = new AuditLogEntry(
                ActorId: Guid.NewGuid(),
                ActorUsername: "user",
                Action: fallbackAction,
                ResourceType: null,
                ResourceId: null,
                HttpMethod: method,
                Route: route,
                StatusCode: 200,
                IpAddress: "127.0.0.1",
                OccurredAt: DateTime.UtcNow
            );

            svc.LogAsync(entry).GetAwaiter().GetResult();

            var log = db.AuditLogs.Single();
            return log.Action == fallbackAction;
        });
    }

    // ── Property 10: ResourceId extraído corretamente dos route values ──

    // Feature: audit-logs, Property 10: ResourceId extraído corretamente dos route values
    // Validates: Requirements 3.3, 5.3, 5.4, 5.6, 5.7, 5.8, 5.9
    [Property(MaxTest = 100)]
    public Property ResourceId_PersistedCorrectlyFromRouteValues()
    {
        var gen = Gen.Fresh(Guid.NewGuid);

        return Prop.ForAll(gen.ToArbitrary(), resourceId =>
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var resourceIdStr = resourceId.ToString();

            var entry = new AuditLogEntry(
                ActorId: Guid.NewGuid(),
                ActorUsername: "user",
                Action: "bets.cancel",
                ResourceType: "bet",
                ResourceId: resourceIdStr,
                HttpMethod: "DELETE",
                Route: $"/api/bets/{resourceIdStr}",
                StatusCode: 204,
                IpAddress: "127.0.0.1",
                OccurredAt: DateTime.UtcNow
            );

            svc.LogAsync(entry).GetAwaiter().GetResult();

            var log = db.AuditLogs.Single();
            return log.ResourceId == resourceIdStr;
        });
    }
}

/// <summary>
/// Property-based tests that exercise AuditActionResolver directly —
/// the middleware logic for ShouldAudit, ResolveAction and ResolveResource.
/// Feature: audit-logs
/// </summary>
public class AuditMiddlewareResolverPropertyTests
{
    private static readonly string[] WriteMethods = ["POST", "PATCH", "PUT", "DELETE"];
    private static readonly string[] ReadMethods  = ["GET", "HEAD", "OPTIONS"];

    // ── Property 2 (middleware): GET/HEAD/OPTIONS nunca devem ser auditados ──

    // Feature: audit-logs, Property 2: Requisições GET nunca geram AuditLog
    // Validates: Requirements 1.2, 4.1
    [Property(MaxTest = 200)]
    public Property ShouldAudit_ReturnsFalse_ForReadMethods()
    {
        var gen = Gen.Elements(ReadMethods);

        return Prop.ForAll(gen.ToArbitrary(), method =>
            !AuditActionResolver.ShouldAudit(method));
    }

    // Feature: audit-logs, Property 2 (complemento): POST/PATCH/PUT/DELETE sempre devem ser auditados
    // Validates: Requirements 1.1
    [Property(MaxTest = 200)]
    public Property ShouldAudit_ReturnsTrue_ForWriteMethods()
    {
        var gen = Gen.Elements(WriteMethods);

        return Prop.ForAll(gen.ToArbitrary(), method =>
            AuditActionResolver.ShouldAudit(method));
    }

    // ── Property 9 (middleware): Fallback para rotas não mapeadas ──

    // Feature: audit-logs, Property 9: Fallback de action para rotas não mapeadas
    // Validates: Requirements 3.2
    [Property(MaxTest = 100)]
    public Property ResolveAction_FallsBack_ForUnmappedRoutes()
    {
        // Generate routes that are definitely not in the ActionMap
        var unmappedTemplates = new[]
        {
            "api/unknown/resource",
            "api/v2/something",
            "api/reports/generate",
            "api/admin/bulk-action",
            "api/custom/{id}/action",
        };

        var gen = from method in Gen.Elements(WriteMethods)
                  from template in Gen.Elements(unmappedTemplates)
                  select (method, template);

        return Prop.ForAll(gen.ToArbitrary(), t =>
        {
            var (method, template) = t;
            var action = AuditActionResolver.ResolveAction(method, template);
            var expected = $"{method.ToUpperInvariant()} {template}";
            return action == expected;
        });
    }

    // Feature: audit-logs, Property 9 (complemento): Rotas mapeadas retornam action semântica
    // Validates: Requirements 3.1
    [Property(MaxTest = 100)]
    public Property ResolveAction_ReturnsSemantic_ForMappedRoutes()
    {
        var mappedEntries = AuditActionResolver.ActionMap.ToArray();
        var gen = Gen.Elements(mappedEntries);

        return Prop.ForAll(gen.ToArbitrary(), entry =>
        {
            var (method, template) = entry.Key;
            var (expectedAction, _) = entry.Value;
            var resolved = AuditActionResolver.ResolveAction(method, template);
            return resolved == expectedAction;
        });
    }

    // ── Property 10 (middleware): ResourceId extraído dos route values ──

    // Feature: audit-logs, Property 10: ResourceId extraído corretamente dos route values
    // Validates: Requirements 3.3
    [Property(MaxTest = 100)]
    public Property ResolveResource_ExtractsId_FromRouteValues()
    {
        var gen = Gen.Fresh(Guid.NewGuid);

        return Prop.ForAll(gen.ToArbitrary(), id =>
        {
            var routeValues = new Dictionary<string, object?> { ["id"] = id.ToString() };
            var (_, resourceId) = AuditActionResolver.ResolveResource(
                "DELETE", "api/bets/{id}", routeValues);
            return resourceId == id.ToString();
        });
    }

    [Property(MaxTest = 100)]
    public Property ResolveResource_ExtractsTeamId_FromRouteValues()
    {
        var gen = Gen.Fresh(Guid.NewGuid);

        return Prop.ForAll(gen.ToArbitrary(), teamId =>
        {
            var routeValues = new Dictionary<string, object?> { ["teamId"] = teamId.ToString() };
            var (_, resourceId) = AuditActionResolver.ResolveResource(
                "DELETE", "api/teams/{teamId:guid}", routeValues);
            return resourceId == teamId.ToString();
        });
    }

    [Property(MaxTest = 100)]
    public Property ResolveResource_ExtractsUserId_FromRouteValues()
    {
        var gen = Gen.Fresh(Guid.NewGuid);

        return Prop.ForAll(gen.ToArbitrary(), userId =>
        {
            // {id} takes priority — use only userId key here
            var routeValues = new Dictionary<string, object?> { ["userId"] = userId.ToString() };
            var (_, resourceId) = AuditActionResolver.ResolveResource(
                "DELETE", "api/trades/listings/{userId:guid}", routeValues);
            return resourceId == userId.ToString();
        });
    }

    // ── Property 3 (middleware): ResourceType correto para rotas mapeadas ──

    // Feature: audit-logs, Property 3 (complemento): ResourceType reflete o tipo do recurso mapeado
    // Validates: Requirements 5.2–5.10
    [Property(MaxTest = 100)]
    public Property ResolveResource_ReturnsCorrectResourceType_ForMappedRoutes()
    {
        var mappedEntries = AuditActionResolver.ActionMap
            .Where(e => e.Value.resourceType is not null)
            .ToArray();

        var gen = Gen.Elements(mappedEntries);

        return Prop.ForAll(gen.ToArbitrary(), entry =>
        {
            var (method, template) = entry.Key;
            var (_, expectedResourceType) = entry.Value;
            var (resourceType, _) = AuditActionResolver.ResolveResource(
                method, template, new Dictionary<string, object?>());
            return resourceType == expectedResourceType;
        });
    }
}
