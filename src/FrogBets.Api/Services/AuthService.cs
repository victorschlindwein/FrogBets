using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FrogBets.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace FrogBets.Api.Services;

public class AuthService : IAuthService
{
    private readonly FrogBetsDbContext _db;
    private readonly IConfiguration _config;
    private readonly TokenBlocklist _blocklist;

    public AuthService(FrogBetsDbContext db, IConfiguration config, TokenBlocklist blocklist)
    {
        _db = db;
        _config = config;
        _blocklist = blocklist;
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        // Always use a generic error — never reveal which field is wrong (Requirement 1.3)
        const string genericError = "Credenciais inválidas";

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username);

        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new UnauthorizedAccessException(genericError);

        var jwtSection = _config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expirationMinutes = int.Parse(jwtSection["ExpirationMinutes"] ?? "60");
        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);
        var jti = Guid.NewGuid().ToString();

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim("isAdmin", user.IsAdmin.ToString().ToLower()),
        };

        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return new AuthResult(tokenString, expiresAt);
    }

    public Task LogoutAsync(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        if (handler.CanReadToken(token))
        {
            var jwt = handler.ReadJwtToken(token);
            var jti = jwt.Id;
            if (!string.IsNullOrEmpty(jti))
                _blocklist.Revoke(jti);
        }
        return Task.CompletedTask;
    }

    public bool IsTokenRevoked(string jti) => _blocklist.IsRevoked(jti);

    public async Task<AuthResult> RegisterAsync(string username, string password, Guid inviteId, Guid? teamId = null)
    {
        if (password.Length < 8)
            throw new InvalidOperationException("PASSWORD_TOO_SHORT");

        var exists = await _db.Users.AnyAsync(u => u.Username == username);
        if (exists)
            throw new InvalidOperationException("USERNAME_TAKEN");

        if (teamId.HasValue)
        {
            var teamExists = await _db.CS2Teams.AnyAsync(t => t.Id == teamId.Value);
            if (!teamExists)
                throw new InvalidOperationException("TEAM_NOT_FOUND");
        }

        var user = new FrogBets.Domain.Entities.User
        {
            Id              = Guid.NewGuid(),
            Username        = username,
            PasswordHash    = BCrypt.Net.BCrypt.HashPassword(password),
            IsAdmin         = false,
            VirtualBalance  = 1000m,
            ReservedBalance = 0m,
            CreatedAt       = DateTime.UtcNow,
            TeamId          = teamId,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Mark invite as used — after user is persisted
        var invite = await _db.Invites.FirstOrDefaultAsync(i => i.Id == inviteId);
        if (invite is not null)
        {
            invite.UsedAt = DateTime.UtcNow;
            invite.UsedByUserId = user.Id;
            await _db.SaveChangesAsync();
        }

        return await LoginAsync(username, password);
    }
}
