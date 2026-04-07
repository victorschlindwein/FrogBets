using System.Text;
using System.Threading.RateLimiting;
using FrogBets.Api.Services;
using FrogBets.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Limita o tamanho máximo do body de qualquer request (1 MB)
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = 1 * 1024 * 1024);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// CORS — permite apenas a origem configurada (ou localhost em dev)
var allowedOrigins = builder.Configuration["AllowedOrigins"]?.Split(',')
    ?? ["http://localhost:3000"];
builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()));

// Rate limiting — proteção contra brute force nos endpoints de auth
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit = 5;
        o.Window = TimeSpan.FromMinutes(15);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 0;
    });
    options.RejectionStatusCode = 429;
});

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

// Team membership service
builder.Services.AddScoped<ITeamMembershipService, TeamMembershipService>();

// Trade service
builder.Services.AddScoped<ITradeService, TradeService>();

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

app.UseCors("Frontend");
app.UseRateLimiter();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }))
   .AllowAnonymous();

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
