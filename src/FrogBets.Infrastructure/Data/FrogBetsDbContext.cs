using FrogBets.Domain.Entities;
using FrogBets.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FrogBets.Infrastructure.Data;

public class FrogBetsDbContext : DbContext
{
    public FrogBetsDbContext(DbContextOptions<FrogBetsDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Market> Markets => Set<Market>();
    public DbSet<Bet> Bets => Set<Bet>();
    public DbSet<GameResult> GameResults => Set<GameResult>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<CS2Team> CS2Teams => Set<CS2Team>();
    public DbSet<CS2Player> CS2Players => Set<CS2Player>();
    public DbSet<MatchStats> MatchStats => Set<MatchStats>();
    public DbSet<TradeListing> TradeListings => Set<TradeListing>();
    public DbSet<TradeOffer> TradeOffers => Set<TradeOffer>();
    public DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Username).IsRequired().HasMaxLength(100);
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.VirtualBalance).HasColumnType("decimal(18,2)");
            e.Property(u => u.ReservedBalance).HasColumnType("decimal(18,2)");
            e.Property(u => u.WinsCount).IsRequired();
            e.Property(u => u.LossesCount).IsRequired();
            e.Property(u => u.CreatedAt).IsRequired();

            e.HasMany(u => u.CreatedBets)
                .WithOne(b => b.Creator)
                .HasForeignKey(b => b.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(u => u.CoveredBets)
                .WithOne(b => b.CoveredBy)
                .HasForeignKey(b => b.CoveredById)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(u => u.Notifications)
                .WithOne(n => n.User)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Game
        modelBuilder.Entity<Game>(e =>
        {
            e.HasKey(g => g.Id);
            e.Property(g => g.TeamA).IsRequired().HasMaxLength(100);
            e.Property(g => g.TeamB).IsRequired().HasMaxLength(100);
            e.Property(g => g.ScheduledAt).IsRequired();
            e.Property(g => g.Status).IsRequired();
            e.Property(g => g.CreatedAt).IsRequired();
            e.HasIndex(g => g.Status);
            e.HasIndex(g => g.ScheduledAt);

            e.HasMany(g => g.Markets)
                .WithOne(m => m.Game)
                .HasForeignKey(m => m.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Market
        modelBuilder.Entity<Market>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Type).IsRequired();
            e.Property(m => m.Status).IsRequired();
            e.HasIndex(m => m.GameId);
            e.HasIndex(m => new { m.GameId, m.Type, m.MapNumber }).IsUnique();

            e.HasMany(m => m.Bets)
                .WithOne(b => b.Market)
                .HasForeignKey(b => b.MarketId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(m => m.GameResults)
                .WithOne(gr => gr.Market)
                .HasForeignKey(gr => gr.MarketId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Bet
        modelBuilder.Entity<Bet>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.CreatorOption).IsRequired().HasMaxLength(200);
            e.Property(b => b.Amount).HasColumnType("decimal(18,2)").IsRequired();
            e.Property(b => b.Status).IsRequired();
            e.Property(b => b.CreatedAt).IsRequired();
            e.HasIndex(b => b.MarketId);
            e.HasIndex(b => b.CreatorId);
            e.HasIndex(b => b.CoveredById);
            e.HasIndex(b => b.Status);
            // Unique: one pending/active bet per user per market
            e.HasIndex(b => new { b.MarketId, b.CreatorId });
        });

        // GameResult
        modelBuilder.Entity<GameResult>(e =>
        {
            e.HasKey(gr => gr.Id);
            e.Property(gr => gr.WinningOption).IsRequired().HasMaxLength(200);
            e.Property(gr => gr.RegisteredAt).IsRequired();
            e.HasIndex(gr => gr.GameId);
            e.HasIndex(gr => gr.MarketId).IsUnique(); // one result per market

            e.HasOne(gr => gr.Game)
                .WithMany()
                .HasForeignKey(gr => gr.GameId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(gr => gr.RegisteredByAdmin)
                .WithMany()
                .HasForeignKey(gr => gr.RegisteredByAdminId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Notification
        modelBuilder.Entity<Notification>(e =>
        {
            e.HasKey(n => n.Id);
            e.Property(n => n.Message).IsRequired().HasMaxLength(500);
            e.Property(n => n.CreatedAt).IsRequired();
            e.HasIndex(n => n.UserId);
            e.HasIndex(n => new { n.UserId, n.IsRead });
        });

        // Invite
        modelBuilder.Entity<Invite>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Token).IsRequired().HasMaxLength(32);
            e.HasIndex(i => i.Token).IsUnique();
            e.Property(i => i.Description).HasMaxLength(200);
            e.Property(i => i.ExpiresAt).IsRequired();
            e.Property(i => i.CreatedAt).IsRequired();

            e.HasOne(i => i.UsedByUser)
                .WithMany()
                .HasForeignKey(i => i.UsedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // CS2Team
        modelBuilder.Entity<CS2Team>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(t => t.Name).IsUnique();
            e.Property(t => t.CreatedAt).IsRequired();

            e.HasMany(t => t.Players)
                .WithOne(p => p.Team)
                .HasForeignKey(p => p.TeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // CS2Player
        modelBuilder.Entity<CS2Player>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Nickname).IsRequired().HasMaxLength(100);
            e.HasIndex(p => p.Nickname).IsUnique();
            e.Property(p => p.PlayerScore).HasDefaultValue(0.0);
            e.Property(p => p.CreatedAt).IsRequired();

            e.HasMany(p => p.Stats)
                .WithOne(s => s.Player)
                .HasForeignKey(s => s.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // MatchStats
        modelBuilder.Entity<MatchStats>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.PlayerId, s.GameId }).IsUnique();
            e.Property(s => s.CreatedAt).IsRequired();

            e.HasOne(s => s.Game)
                .WithMany()
                .HasForeignKey(s => s.GameId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // User → CS2Team FK (TeamId nullable, SetNull on delete)
        modelBuilder.Entity<User>()
            .HasOne(u => u.Team)
            .WithMany()
            .HasForeignKey(u => u.TeamId)
            .OnDelete(DeleteBehavior.SetNull);

        // TradeListing
        modelBuilder.Entity<TradeListing>(e =>
        {
            e.HasKey(tl => tl.Id);
            e.HasIndex(tl => tl.UserId).IsUnique();

            e.HasOne(tl => tl.User)
                .WithMany()
                .HasForeignKey(tl => tl.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(tl => tl.Team)
                .WithMany()
                .HasForeignKey(tl => tl.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TradeOffer
        modelBuilder.Entity<TradeOffer>(e =>
        {
            e.HasKey(o => o.Id);

            e.Property(o => o.Status)
                .HasConversion<string>();

            e.HasOne(o => o.OfferedUser)
                .WithMany()
                .HasForeignKey(o => o.OfferedUserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(o => o.TargetUser)
                .WithMany()
                .HasForeignKey(o => o.TargetUserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(o => o.ProposerTeam)
                .WithMany()
                .HasForeignKey(o => o.ProposerTeamId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(o => o.ReceiverTeam)
                .WithMany()
                .HasForeignKey(o => o.ReceiverTeamId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // RevokedToken
        modelBuilder.Entity<RevokedToken>(e =>
        {
            e.HasKey(r => r.Jti);
            e.Property(r => r.Jti).HasMaxLength(128);
            e.HasIndex(r => r.ExpiresAt); // para limpeza periódica
        });
    }
}
