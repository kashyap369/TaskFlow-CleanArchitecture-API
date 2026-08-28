using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Planner;

namespace TaskFlow.Infra.Persistence.Configurations.Planner;

public sealed class PlannerTemplateConfiguration : IEntityTypeConfiguration<PlannerTemplate>
{
    public void Configure(EntityTypeBuilder<PlannerTemplate> builder)
    {
        builder.ToTable("PlannerTemplates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ObjectType).HasConversion<int>().IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.Icon).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Header).HasMaxLength(120).IsRequired();
        builder.Property(x => x.BackgroundColor).HasMaxLength(7).IsRequired();
        builder.Property(x => x.StrokeColor).HasMaxLength(7).IsRequired();
        builder.Property(x => x.VisibleFieldsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DefaultValuesJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => new { x.ObjectType, x.SortOrder });
        builder.HasMany(x => x.Versions).WithOne(x => x.Template).HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
