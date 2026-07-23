using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Infra.Persistence.Configurations.Organizations
{
    public sealed class TeamConfigurations
        : IEntityTypeConfiguration<Team>
    {
        public void Configure(
            EntityTypeBuilder<Team> builder)
        {
            builder.ToTable("Teams");

            builder.HasKey(x => x.Id);

            builder.Ignore(x => x.DomainEvents);

            builder.Property(x => x.OrganizationId)
                .IsRequired();

            builder.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(500);

            builder.Property(x => x.CreatedByUserId)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .IsRequired();

            builder.HasIndex(x => x.OrganizationId);

            builder.HasIndex(x => new
            {
                x.OrganizationId,
                x.Name
            })
            .IsUnique();

            builder.HasMany(x => x.Members)
                .WithOne()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Metadata
                .FindNavigation(nameof(Team.Members))
                ?.SetPropertyAccessMode(
                    PropertyAccessMode.Field);

            builder.HasQueryFilter(
                x => !x.IsDeleted);
        }
    }
}
