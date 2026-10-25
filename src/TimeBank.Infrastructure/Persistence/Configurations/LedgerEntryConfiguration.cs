using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeBank.Domain.Entities;

namespace TimeBank.Infrastructure.Persistence.Configurations;

public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries", table =>
        {
            table.HasCheckConstraint("CK_LedgerEntries_Hours_NotZero", "[Hours] <> 0");
            table.HasCheckConstraint(
                "CK_LedgerEntries_Hours_HalfStep",
                "[Hours] * 2 = FLOOR([Hours] * 2)");
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Hours).HasPrecision(5, 2).IsRequired();
        builder.Property(l => l.Type).HasConversion<int>().IsRequired();
        builder.Property(l => l.Description).HasMaxLength(300).IsRequired();
        builder.Property(l => l.CreatedAt).IsRequired();

        // This index serves both the balance SUM and the statement's running total.
        builder.HasIndex(l => new { l.MemberId, l.CreatedAt });
        builder.HasIndex(l => new { l.MemberId, l.Type });
        builder.HasIndex(l => l.ReferenceId);
    }
}
