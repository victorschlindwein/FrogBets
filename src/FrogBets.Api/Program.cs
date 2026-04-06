using System.Text;
using FrogBets.Api.Services;
using FrogBets.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// EF Core + PostgreSQL
builder.Services.AddDbContext<FrogBetsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Auth services
builder.Services.AddSingleton<TokenBlocklist>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Balance service
builder.Services.AddScoped<IBalanceService, BalanceService>();

// Game service
builder.Services.AddScoped<IGameService, GameService>();

// Bet service
builder.Services.AddScoped<IBetService, BetService>();

// Settlement service
builder.Services.AddScoped<ISettlementService, SettlementService>();

// Invite service
builder.Services.AddScoped<IInviteService, InviteService>();

// Team service
builder.Services.AddScoped<ITeamService, TeamService>();

// Player service
builder.Services.AddScoped<IPlayerService, PlayerService>();

// Match stats service
builder.Services.AddScoped<IMatchStatsService, MatchStatsService>();

// JWT Bearer authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSection["Key"]!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ClockSkew = TimeSpan.Zero,
        };

        // Check token blocklist on every authenticated request
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var blocklist = context.HttpContext.RequestServices.GetRequiredService<TokenBlocklist>();
                var jti = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
                if (jti is not null && blocklist.IsRevoked(jti))
                {
                    context.Fail("Token has been revoked.");
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Auto-apply EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FrogBetsDbContext>();
    db.Database.Migrate();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
