using System.Security.Claims;
using FrogBets.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrogBets.Api.Controllers;

[ApiController]
[Route("api/marketplace")]
[Authorize]
public class MarketplaceController : ControllerBase
{
    private readonly IBetService _betService;

    public MarketplaceController(IBetService betService)
    {
        _betService = betService;
    }

    /// <summary>GET /api/marketplace — list all Pending bets from other users.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMarketplace()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized();

        var bets = await _betService.GetMarketplaceBetsAsync(userId);
        return Ok(bets);
    }
}
