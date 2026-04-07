using System.Security.Claims;
using FrogBets.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrogBets.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    /// <summary>GET /api/audit-logs — admin: query audit logs with optional filters.</summary>
    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] Guid? actorId,
        [FromQuery] string? action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (User.FindFirstValue("isAdmin") != "true")
            return StatusCode(403, new { error = new { code = "FORBIDDEN", message = "Acesso negado." } });

        var query = new AuditLogQuery(actorId, action, from, to, page, pageSize);
        var result = await _auditLogService.QueryAsync(query);
        return Ok(result);
    }
}
