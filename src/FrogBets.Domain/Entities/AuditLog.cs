namespace FrogBets.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? ActorId { get; set; }
    public string ActorUsername { get; set; } = string.Empty;  // máx 100
    public string Action { get; set; } = string.Empty;         // máx 100
    public string? ResourceType { get; set; }                  // máx 50
    public string? ResourceId { get; set; }                    // máx 100
    public string HttpMethod { get; set; } = string.Empty;     // máx 10
    public string Route { get; set; } = string.Empty;          // máx 200
    public int StatusCode { get; set; }
    public string? IpAddress { get; set; }                     // máx 45 (IPv6)
    public DateTime OccurredAt { get; set; }
    public string? Details { get; set; }                       // máx 1000

    // Navigation (nullable — ActorId pode ser null para anônimos)
    public User? Actor { get; set; }
}
