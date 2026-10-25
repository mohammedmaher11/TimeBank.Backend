using Microsoft.EntityFrameworkCore;
using TimeBank.Domain.Entities;

namespace TimeBank.Infrastructure.Persistence;

public class TimeBankDbContext : DbContext
{
    public TimeBankDbContext(DbContextOptions<TimeBankDbContext> options) : base(options)
    {
    }

    public DbSet<Member> Members => Set<Member>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<MemberSkill> MemberSkills => Set<MemberSkill>();
    public DbSet<HelpRequest> HelpRequests => Set<HelpRequest>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<MemberBadge> MemberBadges => Set<MemberBadge>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TimeBankDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
