using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FrogBets.Api.Services;

public class AuditLogCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogCleanupService> _logger;
    private readonly int _retentionDays;

    public AuditLogCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<AuditLogCleanupService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _retentionDays = configuration.GetValue<int>("AUDIT_LOG_RETENTION_DAYS", 90);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cutoff = DateTime.UtcNow - TimeSpan.FromDays(_retentionDays);

                using var scope = _scopeFactory.CreateScope();
                var auditLogService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

                var deleted = await auditLogService.DeleteExpiredAsync(cutoff);
                _logger.LogInformation("Audit log cleanup: {Count} logs removidos (cutoff: {Cutoff:O})", deleted, cutoff);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante a limpeza de audit logs");
            }

            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}
