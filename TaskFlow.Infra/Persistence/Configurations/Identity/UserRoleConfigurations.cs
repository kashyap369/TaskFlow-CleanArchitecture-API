using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Identity;

namespace TaskFlow.Infra.Persistence.Configurations.Identity
{
    public sealed class UserRoleConfigurations
        : IEntityTypeConfiguration<UserRole>
    {
        public void Configure(
            EntityTypeBuilder<UserRole> builder)
        {
            builder.ToTable("UserRoles");

            builder.HasKey(x => x.Id);

            builder.Ignore(x => x.DomainEvents);

            builder.Property(x => x.UserId)
                .IsRequired();

            builder.Property(x => x.SystemRoleId)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .IsRequired();

            builder.HasIndex(x => x.UserId);

            builder.HasIndex(x => x.SystemRoleId);

            builder.HasIndex(x => new
            {
                x.UserId,
                x.SystemRoleId
            })
            .IsUnique();

            builder.HasQueryFilter(
                x => !x.IsDeleted);
        }
    }
}