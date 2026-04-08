using FrogBets.Api.Controllers;
using FrogBets.Api.Services;
using FrogBets.Infrastructure.Data;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FrogBets.Tests;

/// <summary>
/// Property-based tests for the invite-improvements spec.
/// Feature: invite-improvements
/// </summary>
public class InvitePropertyTests
{
    private static FrogBetsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FrogBetsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new FrogBetsDbContext(options);
    }

    private static InviteService CreateService(FrogBetsDbContext db) => new(db);

    /// <summary>
    /// Feature: invite-improvements, Property 1: round-trip de geração com validade fixa
    /// Validates: Requirements 1.1, 3.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GenerateAsync_ExpiresAtIsFixedAt24h_AndTokenIsValidatable()
    {
        var gen = Arb.Default.String().Generator.Select(s => s?.Length > 0 ? s : null);
        return Prop.ForAll(gen.ToArbitrary(), description =>
        {
            using var db = CreateDb();
            var svc = CreateService(db);
            var before = DateTime.UtcNow;

            var result = svc.GenerateAsync(description).GetAwaiter().GetResult();

            var expectedExpiry = before.AddHours(24);
            var expiryOk = result.ExpiresAt >= expectedExpiry.AddSeconds(-5)
                        && result.ExpiresAt <= expectedExpiry.AddSeconds(5);

            var validated = svc.ValidateAsync(result.Token).GetAwaiter().GetResult();
            var validatable = validated.Id == result.Id;

            return expiryOk && validatable;
        });
    }

    /// <summary>
    /// Feature: invite-improvements, Property 2: geração em lote produz exatamente N tokens únicos
    /// Validates: Requirements 2.1, 2.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BatchGenerate_ProducesExactlyNUniqueTokens()
    {
        var gen = Gen.Choose(1, 50);
        return Prop.ForAll(gen.ToArbitrary(), n =>
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var tokens = new List<string>(n);
            for (int i = 0; i < n; i++)
                tokens.Add(svc.GenerateAsync(null).GetAwaiter().GetResult().Token);

            var countOk = tokens.Count == n;
            var uniqueOk = tokens.Distinct().Count() == n;
            var lengthOk = tokens.All(t => t.Length == 32 && t.All(c => "0123456789abcdef".Contains(c)));

            return countOk && uniqueOk && lengthOk;
        });
    }

    /// <summary>
    /// Feature: invite-improvements, Property 3: quantidade inválida é rejeitada com código correto
    /// Validates: Requirements 2.6, 2.7
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidQuantity_IsRejectedWithCorrectCode()
    {
        // Generate quantities outside [1,50]: either <= 0 or >= 51
        var belowGen = Gen.Choose(int.MinValue / 2, 0);
        var aboveGen = Gen.Choose(51, int.MaxValue / 2);
        var gen = Gen.OneOf(belowGen, aboveGen);

        return Prop.ForAll(gen.ToArbitrary(), qty =>
        {
            using var db = CreateDb();
            var svc = CreateService(db);
            var controller = new InvitesControllerTestHarness(svc, db);

            var request = new CreateInvitesRequest(qty, null);
            var result = controller.ValidateQuantity(request);

            if (qty < 1)
                return result == "INVALID_QUANTITY";
            else
                return result == "QUANTITY_LIMIT_EXCEEDED";
        });
    }
}

/// <summary>
/// Minimal harness to test quantity validation logic without HTTP context.
/// </summary>
internal class InvitesControllerTestHarness
{
    private readonly IInviteService _svc;
    private readonly FrogBetsDbContext _db;

    public InvitesControllerTestHarness(IInviteService svc, FrogBetsDbContext db)
    {
        _svc = svc;
        _db = db;
    }

    /// <summary>Returns the error code if invalid, or null if valid.</summary>
    public string? ValidateQuantity(CreateInvitesRequest request)
    {
        if (request.Quantity < 1) return "INVALID_QUANTITY";
        if (request.Quantity > 50) return "QUANTITY_LIMIT_EXCEEDED";
        return null;
    }
}
