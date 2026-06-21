using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Identity;

namespace TaskFlow.Infra.Persistence.Configurations.Identity
{
    public sealed class SystemRoleConfigurations
        : IEntityTypeConfiguration<SystemRole>
    {
        public void Configure(
            EntityTypeBuilder<SystemRole> builder)
        {
            builder.ToTable("SystemRoles");

            builder.HasKey(x => x.Id);

            builder.Ignore(x => x.DomainEvents);

            builder.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(500);

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .IsRequired();

            builder.HasIndex(x => x.Name)
                .IsUnique();

            builder.HasIndex(x => x.IsDeleted);

            builder.HasQueryFilter(
                x => !x.IsDeleted);
        }
    }
}