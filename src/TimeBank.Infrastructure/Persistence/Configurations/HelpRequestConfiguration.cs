using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Enums;

namespace TimeBank.Infrastructure.Persistence.Configurations;

public sealed class HelpRequestConfiguration : IEntityTypeConfiguration<HelpRequest>
{
    public void Configure(EntityTypeBuilder<HelpRequest> builder)
    {
        builder.ToTable("HelpRequests", table =>
        {
            table.HasCheckConstraint(
                "CK_HelpRequests_EstimatedHours",
                "[EstimatedHours] >= 0.5 AND [EstimatedHours] <= 8 AND [EstimatedHours] * 2 = FLOOR([EstimatedHours] * 2)");
            table.HasCheckConstraint(
                "CK_HelpRequests_AcceptedHours",
                "[AcceptedHours] IS NULL OR ([AcceptedHours] >= 0.5 AND [AcceptedHours] <= 8 AND [AcceptedHours] * 2 = FLOOR([AcceptedHours] * 2))");
            table.HasCheckConstraint(
                "CK_HelpRequests_ActualHours",
                "[ActualHours] IS NULL OR ([ActualHours] >= 0.5 AND [ActualHours] <= 8 AND [ActualHours] * 2 = FLOOR([ActualHours] * 2))");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Title).HasMaxLength(150).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(2000).IsRequired();
        builder.Property(r => r.District).HasMaxLength(100).IsRequired();
        builder.Property(r => r.EstimatedHours).HasPrecision(5, 2).IsRequired();
        builder.Property(r => r.AcceptedHours).HasPrecision(5, 2);
        builder.Property(r => r.ActualHours).HasPrecision(5, 2);
        builder.Property(r => r.NeededBy).HasColumnType("date").IsRequired();
        builder.Property(r => r.Status).HasConversion<int>().IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasMany(r => r.Offers)
            .WithOne(o => o.HelpRequest)
            .HasForeignKey(o => o.HelpRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Offer>()
            .WithMany()
            .HasForeignKey(r => r.AcceptedOfferId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Navigation(r => r.Offers).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(r => new { r.Status, r.CategoryId, r.District });
        builder.HasIndex(r => r.CreatedAt);
        builder.HasIndex(r => r.RequesterId);
        builder.HasIndex(r => r.AcceptedHelperId);
    }
}
