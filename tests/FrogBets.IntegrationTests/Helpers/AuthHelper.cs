using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FrogBets.IntegrationTests.Helpers;

public static class AuthHelper
{
    public const string JwtKey = "super-secret-key-that-is-at-least-32-chars!!";
    private const string Issuer = "FrogBets";
    private const string Audience = "FrogBets";

    public static string GenerateToken(Guid userId, string username, bool isAdmin = false)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("isAdmin", isAdmin.ToString().ToLower()),
        };

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            Issuer, Audience, claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static void SetBearerToken(HttpClient client, Guid userId, string username, bool isAdmin = false)
    {
        var token = GenerateToken(userId, username, isAdmin);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }
}
