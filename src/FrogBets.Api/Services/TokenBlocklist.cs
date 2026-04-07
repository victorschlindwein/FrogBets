using System.Collections.Concurrent;
using FrogBets.Domain.Entities;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Api.Services;

/// <summary>
/// Persistent blocklist for revoked JWT JTI claims backed by the database.
/// Uses an in-memory cache for fast lookups; the DB is the source of truth
/// so revocations survive application restarts.
/// </summary>
public sealed class TokenBlocklist
{
    // Hot cache: populated on startup and updated on every Revoke call
    private readonly ConcurrentDictionary<string, byte> _cache = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private bool _loaded = false;

    public TokenBlocklist(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>Revokes a token by persisting its JTI to the database.</summary>
    public async Task RevokeAsync(string jti, DateTime expiresAt)
    {
        _cache.TryAdd(jti, 0);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FrogBetsDbContext>();

        if (!await db.RevokedTokens.AnyAsync(r => r.Jti == jti))
        {
            db.RevokedTokens.Add(new RevokedToken
            {
                Jti = jti,
                RevokedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
            });
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Checks if a JTI has been revoked. Loads from DB on first call.</summary>
    public bool IsRevoked(string jti)
    {
        EnsureLoaded();
        return _cache.ContainsKey(jti);
    }

    /// <summary>Loads all non-expired revoked JTIs from the DB into the cache (once).</summary>
    private void EnsureLoaded()
    {
        if (_loaded) return;
        lock (this)
        {
            if (_loaded) return;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FrogBetsDbContext>();
            var now = DateTime.UtcNow;
            var active = db.RevokedTokens
                .Where(r => r.ExpiresAt > now)
                .Select(r => r.Jti)
                .ToList();
            foreach (var j in active)
                _cache.TryAdd(j, 0);
            _loaded = true;
        }
    }
}
