using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Platform;

namespace TaskFlow.Infra.Persistence.Configurations.Platform
{
    public sealed class PlatformSettingConfigurations
        : IEntityTypeConfiguration<PlatformSetting>
    {
        public void Configure(
            EntityTypeBuilder<PlatformSetting> builder)
        {
            builder.ToTable("PlatformSettings");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.ApplicationName)
                .HasMaxLength(200)
                .IsRequired();

            // Nullable in the CLR *and* in the database. A
            // non-nullable string silently becomes a NOT NULL column,
            // which has already caused two 500s in this project
            // (RefreshToken revocation fields, TaskWorkLog.Notes).
            builder.Property(x => x.SupportEmail)
                .HasMaxLength(256)
                .IsRequired(false);

            builder.Property(x => x.MaintenanceMessage)
                .HasMaxLength(1000)
                .IsRequired(false);

            builder.Property(x => x.RegistrationOpen)
                .IsRequired();

            builder.Property(x => x.MaintenanceMode)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .IsRequired();

            builder.HasQueryFilter(
                x => !x.IsDeleted);
        }
    }
}
