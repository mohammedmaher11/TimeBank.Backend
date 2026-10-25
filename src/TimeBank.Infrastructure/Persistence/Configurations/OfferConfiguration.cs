using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeBank.Domain.Entities;

namespace TimeBank.Infrastructure.Persistence.Configurations;

public sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> builder)
    {
        builder.ToTable("Offers", table =>
        {
            table.HasCheckConstraint(
                "CK_Offers_Hours",
                "[Hours] >= 0.5 AND [Hours] <= 8 AND [Hours] * 2 = FLOOR([Hours] * 2)");
        });

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Message).HasMaxLength(1000).IsRequired();
        builder.Property(o => o.Hours).HasPrecision(5, 2).IsRequired();
        builder.Property(o => o.Status).HasConversion<int>().IsRequired();
        builder.Property(o => o.CreatedAt).IsRequired();

        builder.HasIndex(o => new { o.HelpRequestId, o.HelperId }).IsUnique();
        builder.HasIndex(o => new { o.HelpRequestId, o.Status });
        builder.HasIndex(o => o.HelperId);
    }
}
