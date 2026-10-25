using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeBank.Domain.Entities;

namespace TimeBank.Infrastructure.Persistence.Configurations;

public sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("Members");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.FullName).HasMaxLength(120).IsRequired();
        builder.Property(m => m.Email).HasMaxLength(200).IsRequired();
        builder.Property(m => m.PasswordHash).HasMaxLength(200).IsRequired();
        builder.Property(m => m.District).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Bio).HasMaxLength(1000);
        builder.Property(m => m.PhotoPath).HasMaxLength(500);
        builder.Property(m => m.Role).HasConversion<int>().IsRequired();
        builder.Property(m => m.IsSuspended).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();

        builder.HasIndex(m => m.Email).IsUnique();
        builder.HasIndex(m => m.District);

        builder.HasMany(m => m.Skills)
            .WithOne(ms => ms.Member)
            .HasForeignKey(ms => ms.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Badges)
            .WithOne(mb => mb.Member)
            .HasForeignKey(mb => mb.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Requests)
            .WithOne(r => r.Requester)
            .HasForeignKey(r => r.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(m => m.Offers)
            .WithOne(o => o.Helper)
            .HasForeignKey(o => o.HelperId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(m => m.LedgerEntries)
            .WithOne(l => l.Member)
            .HasForeignKey(l => l.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(m => m.Skills).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(m => m.Badges).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(m => m.Requests).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(m => m.Offers).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(m => m.LedgerEntries).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
