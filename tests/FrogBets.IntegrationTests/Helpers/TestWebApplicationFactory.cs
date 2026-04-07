using FrogBets.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FrogBets.IntegrationTests.Helpers;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"]               = AuthHelper.JwtKey,
                ["Jwt:Issuer"]            = "FrogBets",
                ["Jwt:Audience"]          = "FrogBets",
                ["Jwt:ExpirationMinutes"] = "60",
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<FrogBetsDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<FrogBetsDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString())
                       .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        });

        builder.UseEnvironment("Test");
    }
}
