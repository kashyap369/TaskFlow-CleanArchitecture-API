using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Identity;

namespace TaskFlow.Infra.Persistence.Configurations.Identity
{
    public sealed class UserConfiguration
        : IEntityTypeConfiguration<User>
    {
        public void Configure(
            EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");

            builder.HasKey(x => x.Id);

            builder.Ignore(x => x.DomainEvents);

            builder.Property(x => x.PasswordHash)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(x => x.Status)
                .HasConversion<int>()
                .IsRequired();

            // Default keeps existing rows valid: every user
            // created before account types is an Individual.
            builder.Property(x => x.AccountType)
                .HasConversion<int>()
                .HasDefaultValue(
                    Domain.Enums.Identity.AccountType.Individual)
                .IsRequired();

            builder.Property(x => x.IsEmailVerified)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .IsRequired();

            builder.Property(x => x.LastLoginAt);

            builder.Property(x => x.UpdatedAt);

            builder.Property(x => x.DeletedAt);

            ConfigureFullName(builder);
            ConfigureEmail(builder);
            ConfigurePhoneNumber(builder);

            ConfigureIndexes(builder);

            builder.HasQueryFilter(x => !x.IsDeleted);
        }

        private static void ConfigureFullName(
            EntityTypeBuilder<User> builder)
        {
            builder.OwnsOne(x => x.FullName, name =>
            {
                name.Property(x => x.FirstName)
                    .HasColumnName("FirstName")
                    .HasMaxLength(100)
                    .IsRequired();

                name.Property(x => x.LastName)
                    .HasColumnName("LastName")
                    .HasMaxLength(100)
                    .IsRequired();
            });
        }

        private static void ConfigureEmail(
    EntityTypeBuilder<User> builder)
        {
            builder.OwnsOne(x => x.Email, email =>
            {
                email.Property(x => x.Value)
                    .HasColumnName("Email")
                    .HasMaxLength(256)
                    .IsRequired();

                email.HasIndex(x => x.Value)
                    .IsUnique();
            });
        }

        private static void ConfigurePhoneNumber(
    EntityTypeBuilder<User> builder)
        {
            builder.OwnsOne(x => x.PhoneNumber, phone =>
            {
                phone.Property(x => x.Value)
                    .HasColumnName("PhoneNumber")
                    .HasMaxLength(20)
                    .IsRequired();

                phone.HasIndex(x => x.Value)
                    .IsUnique();
            });
        }

        private static void ConfigureIndexes(EntityTypeBuilder<User> builder)
        {
            builder.HasIndex(x => x.Status);

            builder.HasIndex(x => x.IsEmailVerified);

            builder.HasIndex(x => x.CreatedAt);

            builder.HasIndex(x => x.LastLoginAt);

            builder.HasIndex(x => x.IsDeleted);

            builder.HasIndex(x => new
            {
                x.IsDeleted,
                x.Status
            });

            builder.HasIndex(x => new
            {
                x.Status,
                x.IsEmailVerified
            });
        }
    }
}