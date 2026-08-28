using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Planner;

namespace TaskFlow.Infra.Persistence.Configurations.Planner;

public sealed class PlannerAssetConfiguration : IEntityTypeConfiguration<PlannerAsset>
{
    public void Configure(EntityTypeBuilder<PlannerAsset> builder)
    {
        builder.ToTable("PlannerAssets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StorageKey).HasMaxLength(512).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(x => x.ScanStatus).HasConversion<int>().IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.BoardId });
    }
}
