using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Organization;
namespace TaskFlow.Infra.Persistence.Configurations.Organizations
{
    public sealed class OrganizationConfigurations
        : IEntityTypeConfiguration<Domain.Entities.Organization.Organization>
    {
        public void Configure(
            EntityTypeBuilder<Domain.Entities.Organization.Organization> builder)
        {
            builder.ToTable("Organizations");

            builder.HasKey(x => x.Id);

            builder.Ignore(x => x.DomainEvents);

            builder.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(1000);

            builder.Property(x => x.OwnerUserId)
                .IsRequired();

            builder.Property(x => x.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .IsRequired();

            builder.HasIndex(x => x.Name);

            builder.HasIndex(x => x.OwnerUserId);

            builder.HasIndex(x => x.Status);

            builder.HasIndex(x => new
            {
                x.OwnerUserId,
                x.Status
            });

            builder.HasIndex(x => new
            {
                x.IsDeleted,
                x.Status
            });

            builder.Metadata
                .FindNavigation(nameof(Domain.Entities.Organization.Organization.Members))
                ?.SetPropertyAccessMode(
                    PropertyAccessMode.Field);

            builder.Metadata
                .FindNavigation(nameof(Domain.Entities.Organization.Organization.Roles))
                ?.SetPropertyAccessMode(
                    PropertyAccessMode.Field);

            builder.HasQueryFilter(
                x => !x.IsDeleted);
        }
    }
}