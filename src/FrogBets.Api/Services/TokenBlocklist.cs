using System.Collections.Concurrent;

namespace FrogBets.Api.Services;

/// <summary>
/// In-memory blocklist for revoked JWT JTI claims.
/// Registered as a singleton so it survives across requests.
/// </summary>
public sealed class TokenBlocklist
{
    private readonly ConcurrentDictionary<string, byte> _revokedJtis = new();

    public void Revoke(string jti) => _revokedJtis.TryAdd(jti, 0);

    public bool IsRevoked(string jti) => _revokedJtis.ContainsKey(jti);
}
