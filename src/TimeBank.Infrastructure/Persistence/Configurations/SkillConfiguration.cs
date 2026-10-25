using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeBank.Domain.Entities;

namespace TimeBank.Infrastructure.Persistence.Configurations;

public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("Skills");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => new { s.CategoryId, s.Name }).IsUnique();
    }
}

public sealed class MemberSkillConfiguration : IEntityTypeConfiguration<MemberSkill>
{
    public void Configure(EntityTypeBuilder<MemberSkill> builder)
    {
        builder.ToTable("MemberSkills");
        builder.HasKey(ms => new { ms.MemberId, ms.SkillId });
        builder.Property(ms => ms.Level).HasConversion<int>().IsRequired();

        builder.HasOne(ms => ms.Skill)
            .WithMany()
            .HasForeignKey(ms => ms.SkillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ms => ms.SkillId);
    }
}
