using FrogBets.Domain.Entities;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Services;

public record AuditLogEntry(
    Guid? ActorId,
    string ActorUsername,
    string Action,
    string? ResourceType,
    string? ResourceId,
    string HttpMethod,
    string Route,
    int StatusCode,
    string? IpAddress,
    DateTime OccurredAt,
    string? Details = null
);

public record AuditLogQuery(
    Guid? ActorId,
    string? Action,
    DateTime? From,
    DateTime? To,
    int Page,
    int PageSize
);

public record AuditLogPage(
    IReadOnlyList<AuditLogDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public record AuditLogDto(
    Guid Id,
    Guid? ActorId,
    string ActorUsername,
    string Action,
    string? ResourceType,
    string? ResourceId,
    string HttpMethod,
    string Route,
    int StatusCode,
    string? IpAddress,
    DateTime OccurredAt,
    string? Details
);

public interface IAuditLogService
{
    Task LogAsync(AuditLogEntry entry);
    Task<AuditLogPage> QueryAsync(AuditLogQuery query);
    Task<int> DeleteExpiredAsync(DateTime cutoff);
}

public class AuditLogService : IAuditLogService
{
    private readonly FrogBetsDbContext _db;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(FrogBetsDbContext db, ILogger<AuditLogService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(AuditLogEntry entry)
    {
        try
        {
            var details = entry.Details is not null && entry.Details.Length > 1000
                ? entry.Details[..1000]
                : entry.Details;

            var log = new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorId = entry.ActorId,
                ActorUsername = entry.ActorUsername,
                Action = entry.Action,
                ResourceType = entry.ResourceType,
                ResourceId = entry.ResourceId,
                HttpMethod = entry.HttpMethod,
                Route = entry.Route,
                StatusCode = entry.StatusCode,
                IpAddress = entry.IpAddress,
                OccurredAt = entry.OccurredAt,
                Details = details
            };

            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist audit log for action {Action} by {ActorUsername}", entry.Action, entry.ActorUsername);
        }
    }

    public async Task<AuditLogPage> QueryAsync(AuditLogQuery query)
    {
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
        var page = Math.Max(query.Page, 1);

        var q = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (query.ActorId.HasValue)
            q = q.Where(a => a.ActorId == query.ActorId.Value);

        if (!string.IsNullOrEmpty(query.Action))
            q = q.Where(a => a.Action.ToLower().Contains(query.Action.ToLower()));

        if (query.From.HasValue)
            q = q.Where(a => a.OccurredAt >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(a => a.OccurredAt <= query.To.Value);

        var totalCount = await q.CountAsync();

        var items = await q
            .OrderByDescending(a => a.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto(
                a.Id,
                a.ActorId,
                a.ActorUsername,
                a.Action,
                a.ResourceType,
                a.ResourceId,
                a.HttpMethod,
                a.Route,
                a.StatusCode,
                a.IpAddress,
                a.OccurredAt,
                a.Details
            ))
            .ToListAsync();

        return new AuditLogPage(items, totalCount, page, pageSize);
    }

    public async Task<int> DeleteExpiredAsync(DateTime cutoff)
    {
        var expired = await _db.AuditLogs
            .Where(a => a.OccurredAt < cutoff)
            .ToListAsync();

        if (expired.Count == 0)
            return 0;

        _db.AuditLogs.RemoveRange(expired);
        await _db.SaveChangesAsync();

        return expired.Count;
    }
}
