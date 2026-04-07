using System.Security.Claims;
using FrogBets.Api.Services;
using Microsoft.AspNetCore.Routing;

namespace FrogBets.Api.Middleware;

/// <summary>
/// Exposes audit resolution logic as public static methods for unit/property testing.
/// </summary>
public static class AuditActionResolver
{
    public static readonly IReadOnlyDictionary<(string method, string template), (string action, string? resourceType)> ActionMap =
        new Dictionary<(string, string), (string, string?)>
        {
            [("POST",   "api/auth/login")]                               = ("auth.login",            null),
            [("POST",   "api/auth/logout")]                              = ("auth.logout",            null),
            [("POST",   "api/auth/register")]                            = ("auth.register",          null),
            [("POST",   "api/bets")]                                     = ("bets.create",            "bet"),
            [("POST",   "api/bets/{id}/cover")]                          = ("bets.cover",             "bet"),
            [("DELETE", "api/bets/{id}")]                                = ("bets.cancel",            "bet"),
            [("POST",   "api/games")]                                    = ("games.create",           null),
            [("PATCH",  "api/games/{id:guid}/start")]                    = ("games.start",            "game"),
            [("POST",   "api/games/{id:guid}/results")]                  = ("games.register_result",  "game"),
            [("POST",   "api/invites")]                                  = ("invites.create",         null),
            [("DELETE", "api/invites/{id:guid}")]                        = ("invites.revoke",         "invite"),
            [("POST",   "api/players")]                                  = ("players.create",         null),
            [("POST",   "api/players/{id:guid}/stats")]                  = ("players.register_stats", "player"),
            [("POST",   "api/teams")]                                    = ("teams.create",           null),
            [("POST",   "api/teams/{teamId:guid}/leader/{userId:guid}")] = ("teams.assign_leader",   "team"),
            [("DELETE", "api/teams/{teamId:guid}/leader")]               = ("teams.remove_leader",   "team"),
            [("DELETE", "api/teams/{teamId:guid}")]                      = ("teams.delete",           "team"),
            [("POST",   "api/trades/listings")]                          = ("trades.add_listing",     null),
            [("DELETE", "api/trades/listings/{userId:guid}")]            = ("trades.remove_listing",  "user"),
            [("POST",   "api/trades/offers")]                            = ("trades.create_offer",    null),
            [("PATCH",  "api/trades/offers/{id:guid}/accept")]           = ("trades.accept_offer",    "trade_offer"),
            [("PATCH",  "api/trades/offers/{id:guid}/reject")]           = ("trades.reject_offer",    "trade_offer"),
            [("POST",   "api/trades/direct")]                            = ("trades.direct_swap",     null),
            [("PATCH",  "api/users/{id:guid}/team")]                     = ("users.move_team",        "user"),
            [("POST",   "api/users/{id:guid}/promote")]                  = ("users.promote",          "user"),
            [("POST",   "api/users/{id:guid}/demote")]                   = ("users.demote",           "user"),
        };

    public static bool ShouldAudit(string httpMethod) =>
        HttpMethods.IsPost(httpMethod)
        || HttpMethods.IsPatch(httpMethod)
        || HttpMethods.IsPut(httpMethod)
        || HttpMethods.IsDelete(httpMethod);

    public static string ResolveAction(string method, string? routeTemplate)
    {
        var key = (method.ToUpperInvariant(), routeTemplate ?? string.Empty);
        return ActionMap.TryGetValue(key, out var mapped)
            ? mapped.action
            : $"{method.ToUpperInvariant()} {routeTemplate ?? string.Empty}";
    }

    public static (string? resourceType, string? resourceId) ResolveResource(
        string method, string? routeTemplate, IReadOnlyDictionary<string, object?> routeValues)
    {
        string? resourceType = null;
        if (routeTemplate is not null && ActionMap.TryGetValue((method.ToUpperInvariant(), routeTemplate), out var mapped))
            resourceType = mapped.resourceType;

        string? resourceId = null;
        if (routeValues.TryGetValue("id", out var idVal) && idVal is not null)
            resourceId = idVal.ToString();
        else if (routeValues.TryGetValue("teamId", out var teamIdVal) && teamIdVal is not null)
            resourceId = teamIdVal.ToString();
        else if (routeValues.TryGetValue("userId", out var userIdVal) && userIdVal is not null)
            resourceId = userIdVal.ToString();

        return (resourceType, resourceId);
    }
}

public class AuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditMiddleware> _logger;

    public AuditMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory, ILogger<AuditMiddleware> logger)
    {
        _next = next;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!AuditActionResolver.ShouldAudit(context.Request.Method))
        {
            await _next(context);
            return;
        }

        int capturedStatusCode = 200;
        context.Response.OnStarting(() =>
        {
            capturedStatusCode = context.Response.StatusCode;
            return Task.CompletedTask;
        });

        await _next(context);

        var routeTemplate = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern?.RawText;
        var method = context.Request.Method;

        var action = AuditActionResolver.ResolveAction(method, routeTemplate);

        var routeValues = context.Request.RouteValues
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        var (resourceType, resourceId) = AuditActionResolver.ResolveResource(method, routeTemplate, routeValues);

        Guid? actorId = null;
        string actorUsername = "anonymous";

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var idStr = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? context.User.FindFirstValue("sub");
            if (idStr is not null && Guid.TryParse(idStr, out var parsedId))
                actorId = parsedId;

            actorUsername = context.User.FindFirstValue(ClaimTypes.Name)
                         ?? context.User.FindFirstValue("unique_name")
                         ?? "anonymous";
        }

        var ipAddress = context.Connection.RemoteIpAddress?.ToString();

        var entry = new AuditLogEntry(
            ActorId: actorId,
            ActorUsername: actorUsername,
            Action: action,
            ResourceType: resourceType,
            ResourceId: resourceId,
            HttpMethod: method,
            Route: routeTemplate ?? context.Request.Path.Value ?? string.Empty,
            StatusCode: capturedStatusCode,
            IpAddress: ipAddress,
            OccurredAt: DateTime.UtcNow
        );

        FireAndForgetLog(_scopeFactory, entry, _logger);
    }

    private static void FireAndForgetLog(IServiceScopeFactory scopeFactory, AuditLogEntry entry, ILogger logger)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
                await service.LogAsync(entry);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fire-and-forget audit log for action {Action} by {ActorUsername}",
                    entry.Action, entry.ActorUsername);
            }
        });
    }
}
