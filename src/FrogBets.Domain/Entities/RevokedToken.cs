namespace FrogBets.Domain.Entities;

/// <summary>Represents a revoked JWT identified by its JTI claim.</summary>
public class RevokedToken
{
    public string Jti { get; set; } = string.Empty;
    public DateTime RevokedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
