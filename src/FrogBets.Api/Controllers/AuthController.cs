using System.Security.Claims;
using FrogBets.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrogBets.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IInviteService _inviteService;

    public AuthController(IAuthService authService, IInviteService inviteService)
    {
        _authService = authService;
        _inviteService = inviteService;
    }

    /// <summary>POST /api/auth/login — returns JWT token on valid credentials.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await _authService.LoginAsync(request.Username, request.Password);
            return Ok(new { token = result.Token, expiresAt = result.ExpiresAt });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new
            {
                error = new { code = "INVALID_CREDENTIALS", message = ex.Message }
            });
        }
    }

    /// <summary>POST /api/auth/logout — invalidates the current bearer token.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader["Bearer ".Length..].Trim()
            : string.Empty;

        if (!string.IsNullOrEmpty(token))
            await _authService.LogoutAsync(token);

        return NoContent();
    }

    /// <summary>POST /api/auth/register — creates a new user using a valid invite token.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterWithInviteRequest request)
    {
        Guid inviteId;
        try
        {
            var invite = await _inviteService.ValidateAsync(request.InviteToken);
            inviteId = invite.Id;
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVALID_INVITE")
        {
            return BadRequest(new { error = new { code = "INVALID_INVITE", message = "Convite inválido ou expirado." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVITE_ALREADY_USED")
        {
            return BadRequest(new { error = new { code = "INVITE_ALREADY_USED", message = "Este convite já foi utilizado." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVITE_EXPIRED")
        {
            return BadRequest(new { error = new { code = "INVALID_INVITE", message = "Convite inválido ou expirado." } });
        }

        try
        {
            var result = await _authService.RegisterAsync(request.Username, request.Password, inviteId);
            return Ok(new { token = result.Token, expiresAt = result.ExpiresAt });
        }
        catch (InvalidOperationException ex) when (ex.Message == "USERNAME_TAKEN")
        {
            return Conflict(new { error = new { code = "USERNAME_TAKEN", message = "Nome de usuário já está em uso." } });
        }
        catch (InvalidOperationException ex) when (ex.Message == "PASSWORD_TOO_SHORT")
        {
            return BadRequest(new { error = new { code = "PASSWORD_TOO_SHORT", message = "A senha deve ter no mínimo 8 caracteres." } });
        }
    }
}

public record LoginRequest(string Username, string Password);
public record RegisterWithInviteRequest(string InviteToken, string Username, string Password);
