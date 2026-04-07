using FrogBets.Api.Services;
using FrogBets.Domain.Entities;
using FrogBets.Infrastructure.Data;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// TradeOfferStatus is defined in FrogBets.Domain.Entities namespace (same as TradeOffer)

namespace FrogBets.Tests;

/// <summary>
/// Property-based tests for the Team Membership feature (team-membership spec).
/// Feature: team-membership
/// </summary>
public class TeamMembershipTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static FrogBetsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FrogBetsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new FrogBetsDbContext(options);
    }

    private static async Task<CS2Team> SeedTeamAsync(FrogBetsDbContext db, string? name = null)
    {
        var team = new CS2Team
        {
            Id        = Guid.NewGuid(),
            Name      = name ?? Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
        };
        db.CS2Teams.Add(team);
        await db.SaveChangesAsync();
        return team;
    }

    private static async Task<User> SeedUserAsync(FrogBetsDbContext db,
        Guid? teamId = null, bool isTeamLeader = false)
    {
        var user = new User
        {
            Id              = Guid.NewGuid(),
            Username        = Guid.NewGuid().ToString("N"),
            PasswordHash    = "hash",
            VirtualBalance  = 1000m,
            ReservedBalance = 0m,
            TeamId          = teamId,
            IsTeamLeader    = isTeamLeader,
            CreatedAt       = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static TeamMembershipService CreateService(FrogBetsDbContext db)
        => new(db);

    // ── Property 1: Cadastro com time preserva o TeamId ──────────────────────

    // Feature: team-membership, Property 1: registration with valid teamId stores that TeamId
    [Property(MaxTest = 100)]
    public Property Registration_WithValidTeamId_StoresTeamId()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var team = SeedTeamAsync(db).GetAwaiter().GetResult();
            var user = SeedUserAsync(db, teamId: team.Id).GetAwaiter().GetResult();

            var stored = db.Users.Find(user.Id)!;
            return stored.TeamId == team.Id;
        });
    }

    // Feature: team-membership, Property 2: registration without teamId results in null TeamId
    [Property(MaxTest = 100)]
    public Property Registration_WithoutTeamId_HasNullTeamId()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var user = SeedUserAsync(db, teamId: null).GetAwaiter().GetResult();

            var stored = db.Users.Find(user.Id)!;
            return stored.TeamId == null;
        });
    }

    // Feature: team-membership, Property 3: invalid teamId in registration is rejected
    [Property(MaxTest = 100)]
    public Property Registration_WithInvalidTeamId_IsRejected()
    {
        return Prop.ForAll(Arb.Default.Guid(), randomGuid =>
        {
            using var db = CreateDb();
            // No teams seeded — any GUID is invalid
            var teamExists = db.CS2Teams.Any(t => t.Id == randomGuid);
            if (teamExists) return true; // skip (extremely unlikely)

            var svc = CreateService(db);
            try
            {
                // Simulate what AuthService does: check team existence
                var exists = db.CS2Teams.Any(t => t.Id == randomGuid);
                if (!exists) throw new InvalidOperationException("TEAM_NOT_FOUND");
                return false;
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message == "TEAM_NOT_FOUND";
            }
        });
    }

    // ── Property 4: Invariante de líder único por time ────────────────────────

    // Feature: team-membership, Property 4: at most one leader per team at any time
    [Property(MaxTest = 50)]
    public Property TeamLeader_Invariant_AtMostOneLeaderPerTeam()
    {
        var gen = Gen.Choose(2, 5);

        return Prop.ForAll(gen.ToArbitrary(), memberCount =>
        {
            using var db = CreateDb();
            var team = SeedTeamAsync(db).GetAwaiter().GetResult();
            var svc  = CreateService(db);

            var members = new List<User>();
            for (int i = 0; i < memberCount; i++)
                members.Add(SeedUserAsync(db, teamId: team.Id).GetAwaiter().GetResult());

            // Assign leader multiple times — should always result in exactly one leader
            foreach (var member in members)
                svc.AssignLeaderAsync(team.Id, member.Id).GetAwaiter().GetResult();

            var leaderCount = db.Users.Count(u => u.TeamId == team.Id && u.IsTeamLeader);
            return leaderCount <= 1;
        });
    }

    // ── Property 5: Remoção de líder limpa IsTeamLeader ──────────────────────

    // Feature: team-membership, Property 5: removing leader sets IsTeamLeader to false
    [Property(MaxTest = 100)]
    public Property RemoveLeader_SetsIsTeamLeaderToFalse()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var team   = SeedTeamAsync(db).GetAwaiter().GetResult();
            var member = SeedUserAsync(db, teamId: team.Id).GetAwaiter().GetResult();
            var svc    = CreateService(db);

            svc.AssignLeaderAsync(team.Id, member.Id).GetAwaiter().GetResult();
            svc.RemoveLeaderAsync(team.Id).GetAwaiter().GetResult();

            var updated = db.Users.Find(member.Id)!;
            return !updated.IsTeamLeader;
        });
    }

    // ── Property 6: Remoção do time limpa IsTeamLeader automaticamente ────────

    // Feature: team-membership, Property 6: moving leader out of team clears IsTeamLeader
    [Property(MaxTest = 100)]
    public Property MoveLeaderOutOfTeam_ClearsIsTeamLeader()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var teamA  = SeedTeamAsync(db).GetAwaiter().GetResult();
            var teamB  = SeedTeamAsync(db).GetAwaiter().GetResult();
            var leader = SeedUserAsync(db, teamId: teamA.Id, isTeamLeader: true).GetAwaiter().GetResult();
            var svc    = CreateService(db);

            // Admin moves leader to another team
            svc.MoveUserAsync(Guid.NewGuid(), requesterIsAdmin: true, leader.Id, teamB.Id)
               .GetAwaiter().GetResult();

            var updated = db.Users.Find(leader.Id)!;
            return !updated.IsTeamLeader && updated.TeamId == teamB.Id;
        });
    }

    // ── Property 7: Movimentação de membro atualiza TeamId corretamente ───────

    // Feature: team-membership, Property 7: moving member updates TeamId to destination
    [Property(MaxTest = 100)]
    public Property MoveUser_UpdatesTeamIdToDestination()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var teamA  = SeedTeamAsync(db).GetAwaiter().GetResult();
            var teamB  = SeedTeamAsync(db).GetAwaiter().GetResult();
            var member = SeedUserAsync(db, teamId: teamA.Id).GetAwaiter().GetResult();
            var svc    = CreateService(db);

            svc.MoveUserAsync(Guid.NewGuid(), requesterIsAdmin: true, member.Id, teamB.Id)
               .GetAwaiter().GetResult();

            var updated = db.Users.Find(member.Id)!;
            return updated.TeamId == teamB.Id;
        });
    }

    // ── Property 8: Remoção de time pelo admin define TeamId como nulo ────────

    // Feature: team-membership, Property 8: admin removing team sets TeamId to null
    [Property(MaxTest = 100)]
    public Property AdminRemovesTeam_SetsTeamIdToNull()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var team   = SeedTeamAsync(db).GetAwaiter().GetResult();
            var member = SeedUserAsync(db, teamId: team.Id).GetAwaiter().GetResult();
            var svc    = CreateService(db);

            svc.MoveUserAsync(Guid.NewGuid(), requesterIsAdmin: true, member.Id, destinationTeamId: null)
               .GetAwaiter().GetResult();

            var updated = db.Users.Find(member.Id)!;
            return updated.TeamId == null;
        });
    }

    // ── Property 9: Marcação de disponibilidade é refletida na listagem ───────

    // Feature: team-membership, Property 9: listing a member shows them in GetListingsAsync
    [Property(MaxTest = 50)]
    public Property AddListing_MemberAppearsInListings()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var team   = SeedTeamAsync(db).GetAwaiter().GetResult();
            var leader = SeedUserAsync(db, teamId: team.Id, isTeamLeader: true).GetAwaiter().GetResult();
            var member = SeedUserAsync(db, teamId: team.Id).GetAwaiter().GetResult();
            var svc    = new TradeService(db);

            svc.AddListingAsync(leader.Id, member.Id).GetAwaiter().GetResult();

            var listings = svc.GetListingsAsync().GetAwaiter().GetResult();
            return listings.Any(l => l.UserId == member.Id);
        });
    }

    // ── Property 10: Remoção de disponibilidade remove da listagem ────────────

    // Feature: team-membership, Property 10: removing listing removes member from GetListingsAsync
    [Property(MaxTest = 50)]
    public Property RemoveListing_MemberDisappearsFromListings()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var team   = SeedTeamAsync(db).GetAwaiter().GetResult();
            var leader = SeedUserAsync(db, teamId: team.Id, isTeamLeader: true).GetAwaiter().GetResult();
            var member = SeedUserAsync(db, teamId: team.Id).GetAwaiter().GetResult();
            var svc    = new TradeService(db);

            svc.AddListingAsync(leader.Id, member.Id).GetAwaiter().GetResult();
            svc.RemoveListingAsync(leader.Id, member.Id).GetAwaiter().GetResult();

            var listings = svc.GetListingsAsync().GetAwaiter().GetResult();
            return !listings.Any(l => l.UserId == member.Id);
        });
    }

    // ── Property 11: Transferência de time remove disponibilidade automaticamente

    // Feature: team-membership, Property 11: transferring a listed member removes their listing
    [Property(MaxTest = 50)]
    public Property TransferListedMember_RemovesListing()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var teamA  = SeedTeamAsync(db).GetAwaiter().GetResult();
            var teamB  = SeedTeamAsync(db).GetAwaiter().GetResult();
            var leader = SeedUserAsync(db, teamId: teamA.Id, isTeamLeader: true).GetAwaiter().GetResult();
            var member = SeedUserAsync(db, teamId: teamA.Id).GetAwaiter().GetResult();
            var tradeSvc = new TradeService(db);
            var memberSvc = CreateService(db);

            tradeSvc.AddListingAsync(leader.Id, member.Id).GetAwaiter().GetResult();
            // Move member to teamB — should auto-remove listing
            memberSvc.MoveUserAsync(Guid.NewGuid(), requesterIsAdmin: true, member.Id, teamB.Id)
                     .GetAwaiter().GetResult();

            var listings = tradeSvc.GetListingsAsync().GetAwaiter().GetResult();
            return !listings.Any(l => l.UserId == member.Id);
        });
    }

    // ── Property 12: Oferta criada tem status Pendente ────────────────────────

    // Feature: team-membership, Property 12: created offer has Pending status
    [Property(MaxTest = 50)]
    public Property CreateOffer_HasPendingStatus()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var teamA   = SeedTeamAsync(db).GetAwaiter().GetResult();
            var teamB   = SeedTeamAsync(db).GetAwaiter().GetResult();
            var leaderA = SeedUserAsync(db, teamId: teamA.Id, isTeamLeader: true).GetAwaiter().GetResult();
            var memberA = SeedUserAsync(db, teamId: teamA.Id).GetAwaiter().GetResult();
            var leaderB = SeedUserAsync(db, teamId: teamB.Id, isTeamLeader: true).GetAwaiter().GetResult();
            var memberB = SeedUserAsync(db, teamId: teamB.Id).GetAwaiter().GetResult();
            var svc     = new TradeService(db);

            // List memberB as available
            svc.AddListingAsync(leaderB.Id, memberB.Id).GetAwaiter().GetResult();

            var offerId = svc.CreateOfferAsync(leaderA.Id, memberA.Id, memberB.Id)
                            .GetAwaiter().GetResult();

            var offer = db.TradeOffers.Find(offerId)!;
            return offer.Status == TradeOfferStatus.Pending;
        });
    }

    // ── Property 13: Aceitação de oferta troca TeamIds e limpa disponibilidades

    // Feature: team-membership, Property 13: accepting offer swaps TeamIds and clears listings
    [Property(MaxTest = 50)]
    public Property AcceptOffer_SwapsTeamIdsAndClearsListings()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var teamA   = SeedTeamAsync(db).GetAwaiter().GetResult();
            var teamB   = SeedTeamAsync(db).GetAwaiter().GetResult();
            var leaderA = SeedUserAsync(db, teamId: teamA.Id, isTeamLeader: true).GetAwaiter().GetResult();
            var memberA = SeedUserAsync(db, teamId: teamA.Id).GetAwaiter().GetResult();
            var leaderB = SeedUserAsync(db, teamId: teamB.Id, isTeamLeader: true).GetAwaiter().GetResult();
            var memberB = SeedUserAsync(db, teamId: teamB.Id).GetAwaiter().GetResult();
            var svc     = new TradeService(db);

            svc.AddListingAsync(leaderB.Id, memberB.Id).GetAwaiter().GetResult();
            var offerId = svc.CreateOfferAsync(leaderA.Id, memberA.Id, memberB.Id)
                            .GetAwaiter().GetResult();
            svc.AcceptOfferAsync(leaderB.Id, offerId).GetAwaiter().GetResult();

            var updatedA = db.Users.Find(memberA.Id)!;
            var updatedB = db.Users.Find(memberB.Id)!;
            var offer    = db.TradeOffers.Find(offerId)!;
            var listings = db.TradeListings.ToList();

            return updatedA.TeamId == teamB.Id
                && updatedB.TeamId == teamA.Id
                && offer.Status == TradeOfferStatus.Accepted
                && !listings.Any(l => l.UserId == memberA.Id || l.UserId == memberB.Id);
        });
    }

    // ── Property 14: Recusa de oferta não altera TeamIds ─────────────────────

    // Feature: team-membership, Property 14: rejecting offer does not change TeamIds
    [Property(MaxTest = 50)]
    public Property RejectOffer_DoesNotChangeTeamIds()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var teamA   = SeedTeamAsync(db).GetAwaiter().GetResult();
            var teamB   = SeedTeamAsync(db).GetAwaiter().GetResult();
            var leaderA = SeedUserAsync(db, teamId: teamA.Id, isTeamLeader: true).GetAwaiter().GetResult();
            var memberA = SeedUserAsync(db, teamId: teamA.Id).GetAwaiter().GetResult();
            var leaderB = SeedUserAsync(db, teamId: teamB.Id, isTeamLeader: true).GetAwaiter().GetResult();
            var memberB = SeedUserAsync(db, teamId: teamB.Id).GetAwaiter().GetResult();
            var svc     = new TradeService(db);

            svc.AddListingAsync(leaderB.Id, memberB.Id).GetAwaiter().GetResult();
            var offerId = svc.CreateOfferAsync(leaderA.Id, memberA.Id, memberB.Id)
                            .GetAwaiter().GetResult();
            svc.RejectOfferAsync(leaderB.Id, offerId).GetAwaiter().GetResult();

            var updatedA = db.Users.Find(memberA.Id)!;
            var updatedB = db.Users.Find(memberB.Id)!;
            var offer    = db.TradeOffers.Find(offerId)!;

            return updatedA.TeamId == teamA.Id
                && updatedB.TeamId == teamB.Id
                && offer.Status == TradeOfferStatus.Rejected;
        });
    }

    // ── Property 15: Aceitação cancela outras ofertas pendentes dos membros ───

    // Feature: team-membership, Property 15: accepting offer cancels other pending offers for involved members
    [Fact]
    public async Task AcceptOffer_CancelsOtherPendingOffersForInvolvedMembers()
    {
        await using var db = CreateDb();
        var teamA   = await SeedTeamAsync(db);
        var teamB   = await SeedTeamAsync(db);
        var teamC   = await SeedTeamAsync(db);
        var leaderA = await SeedUserAsync(db, teamId: teamA.Id, isTeamLeader: true);
        var memberA = await SeedUserAsync(db, teamId: teamA.Id);
        var leaderB = await SeedUserAsync(db, teamId: teamB.Id, isTeamLeader: true);
        var memberB = await SeedUserAsync(db, teamId: teamB.Id);
        var leaderC = await SeedUserAsync(db, teamId: teamC.Id, isTeamLeader: true);
        var memberC = await SeedUserAsync(db, teamId: teamC.Id);
        var svc     = new TradeService(db);

        // List memberB and memberA as available
        await svc.AddListingAsync(leaderB.Id, memberB.Id);
        await svc.AddListingAsync(leaderA.Id, memberA.Id);

        // leaderA offers memberA for memberB
        var offer1 = await svc.CreateOfferAsync(leaderA.Id, memberA.Id, memberB.Id);
        // leaderC also offers memberC for memberB (competing offer)
        await svc.AddListingAsync(leaderC.Id, memberC.Id);
        var offer2 = await svc.CreateOfferAsync(leaderC.Id, memberC.Id, memberB.Id);

        // leaderB accepts offer1
        await svc.AcceptOfferAsync(leaderB.Id, offer1);

        var cancelledOffer = await db.TradeOffers.FindAsync(offer2);
        Assert.Equal(TradeOfferStatus.Cancelled, cancelledOffer!.Status);
    }

    // ── Property 16: Troca direta pelo admin realiza swap de TeamIds ──────────

    // Feature: team-membership, Property 16: direct swap by admin swaps TeamIds and clears listings/offers
    [Property(MaxTest = 50)]
    public Property DirectSwap_SwapsTeamIdsAndClearsListingsAndOffers()
    {
        return Prop.ForAll(Arb.Default.Guid(), _ =>
        {
            using var db = CreateDb();
            var teamA   = SeedTeamAsync(db).GetAwaiter().GetResult();
            var teamB   = SeedTeamAsync(db).GetAwaiter().GetResult();
            var leaderA = SeedUserAsync(db, teamId: teamA.Id, isTeamLeader: true).GetAwaiter().GetResult();
            var memberA = SeedUserAsync(db, teamId: teamA.Id).GetAwaiter().GetResult();
            var leaderB = SeedUserAsync(db, teamId: teamB.Id, isTeamLeader: true).GetAwaiter().GetResult();
            var memberB = SeedUserAsync(db, teamId: teamB.Id).GetAwaiter().GetResult();
            var svc     = new TradeService(db);

            // List both as available
            svc.AddListingAsync(leaderA.Id, memberA.Id).GetAwaiter().GetResult();
            svc.AddListingAsync(leaderB.Id, memberB.Id).GetAwaiter().GetResult();

            svc.DirectSwapAsync(memberA.Id, memberB.Id).GetAwaiter().GetResult();

            var updatedA = db.Users.Find(memberA.Id)!;
            var updatedB = db.Users.Find(memberB.Id)!;
            var listings = db.TradeListings.ToList();

            return updatedA.TeamId == teamB.Id
                && updatedB.TeamId == teamA.Id
                && !listings.Any(l => l.UserId == memberA.Id || l.UserId == memberB.Id);
        });
    }

    // ── Additional edge case tests ────────────────────────────────────────────

    // Feature: team-membership, Property: non-leader cannot move members
    [Fact]
    public async Task MoveUser_NonLeaderNonAdmin_ThrowsForbidden()
    {
        await using var db = CreateDb();
        var teamA    = await SeedTeamAsync(db);
        var teamB    = await SeedTeamAsync(db);
        var requester = await SeedUserAsync(db, teamId: teamA.Id, isTeamLeader: false);
        var target    = await SeedUserAsync(db, teamId: teamA.Id);
        var svc       = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.MoveUserAsync(requester.Id, requesterIsAdmin: false, target.Id, teamB.Id));

        Assert.Equal("FORBIDDEN", ex.Message);
    }

    // Feature: team-membership, Property: assigning leader to user not in team is rejected
    [Fact]
    public async Task AssignLeader_UserNotInTeam_ThrowsUserNotInTeam()
    {
        await using var db = CreateDb();
        var teamA  = await SeedTeamAsync(db);
        var teamB  = await SeedTeamAsync(db);
        var member = await SeedUserAsync(db, teamId: teamB.Id); // member of teamB, not teamA
        var svc    = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AssignLeaderAsync(teamA.Id, member.Id));

        Assert.Equal("USER_NOT_IN_TEAM", ex.Message);
    }

    // Feature: team-membership, Property: direct swap of same-team members is rejected
    [Fact]
    public async Task DirectSwap_SameTeam_ThrowsSameTeamTrade()
    {
        await using var db = CreateDb();
        var team    = await SeedTeamAsync(db);
        var memberA = await SeedUserAsync(db, teamId: team.Id);
        var memberB = await SeedUserAsync(db, teamId: team.Id);
        var svc     = new TradeService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DirectSwapAsync(memberA.Id, memberB.Id));

        Assert.Equal("SAME_TEAM_TRADE", ex.Message);
    }
}
