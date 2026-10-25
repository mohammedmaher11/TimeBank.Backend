using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeBank.Domain.Entities;

namespace TimeBank.Infrastructure.Persistence.Configurations;

public sealed class BadgeConfiguration : IEntityTypeConfiguration<Badge>
{
    public void Configure(EntityTypeBuilder<Badge> builder)
    {
        builder.ToTable("Badges");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Code).HasMaxLength(50).IsRequired();
        builder.Property(b => b.Name).HasMaxLength(100).IsRequired();
        builder.Property(b => b.Icon).HasMaxLength(20).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(300).IsRequired();
        builder.HasIndex(b => b.Code).IsUnique();
    }
}

public sealed class MemberBadgeConfiguration : IEntityTypeConfiguration<MemberBadge>
{
    public void Configure(EntityTypeBuilder<MemberBadge> builder)
    {
        builder.ToTable("MemberBadges");
        builder.HasKey(mb => new { mb.MemberId, mb.BadgeId });
        builder.Property(mb => mb.EarnedAt).IsRequired();

        builder.HasOne(mb => mb.Badge)
            .WithMany(b => b.MemberBadges)
            .HasForeignKey(mb => mb.BadgeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(mb => mb.BadgeId);
        builder.Navigation(mb => mb.Badge).UsePropertyAccessMode(PropertyAccessMode.Property);
    }
}
