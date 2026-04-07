using FrogBets.Domain.Entities;
using FrogBets.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace FrogBets.Tests.Integration;

/// <summary>
/// WebApplicationFactory configurada com banco InMemory para testes de integração.
/// Cada instância usa um banco isolado via Guid único.
/// </summary>
public class IntegrationTestFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove o DbContext real e substitui pelo InMemory
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<FrogBetsDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<FrogBetsDbContext>(options =>
                options.UseInMemoryDatabase(_dbName)
                       .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        });

        builder.UseEnvironment("Testing");
    }

    /// <summary>Cria um scope e retorna o DbContext para seed de dados.</summary>
    public FrogBetsDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<FrogBetsDbContext>();
    }

    /// <summary>Gera um JWT válido para o usuário informado usando a chave de teste.</summary>
    public static string GenerateToken(Guid userId, string username, bool isAdmin = false)
    {
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes("super-secret-key-that-is-at-least-32-chars!!"));
        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new System.Security.Claims.Claim(
                System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, userId.ToString()),
            new System.Security.Claims.Claim(
                System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName, username),
            new System.Security.Claims.Claim(
                System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new System.Security.Claims.Claim("isAdmin", isAdmin.ToString().ToLower()),
        };

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "FrogBets",
            audience: "FrogBets",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Seed de usuário padrão com saldo inicial.</summary>
    public static async Task<User> SeedUserAsync(FrogBetsDbContext db,
        string username = "testuser", bool isAdmin = false, decimal balance = 1000m)
    {
        var user = new User
        {
            Id              = Guid.NewGuid(),
            Username        = username,
            PasswordHash    = BCrypt.Net.BCrypt.HashPassword("password123"),
            IsAdmin         = isAdmin,
            VirtualBalance  = balance,
            ReservedBalance = 0m,
            CreatedAt       = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
