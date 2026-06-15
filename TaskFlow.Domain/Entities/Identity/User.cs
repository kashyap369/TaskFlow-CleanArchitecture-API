using System;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.DomainEvents.Identity.User;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Entities.Identity
{
    public class User : AuditableEntity, IAggregateRoot
    {
        public FullName Name { get; private set; }

        public Email Email { get; private set; }

        public PhoneNumber PhoneNumber { get; private set; }

        public string PasswordHash { get; private set; }

        public AccountType AccountType { get; private set; }

        public UserStatus Status { get; private set; }

        public bool IsEmailVerified { get; private set; }

        public DateTime? LastLoginAt { get; private set; }

        private User()
        {
        }

        private User(
            FullName name,
            Email email,
            PhoneNumber phoneNumber,
            string passwordHash,
            AccountType accountType)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));

            Email = email ?? throw new ArgumentNullException(nameof(email));

            PhoneNumber = phoneNumber ?? throw new ArgumentNullException(nameof(phoneNumber));

            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException(
                    "Password hash is required.",
                    nameof(passwordHash));

            PasswordHash = passwordHash;

            AccountType = accountType;

            Status = UserStatus.PendingVerification;

            IsEmailVerified = false;
        }

        public static User Register(
            FullName name,
            Email email,
            PhoneNumber phoneNumber,
            string passwordHash,
            AccountType accountType)
        {
            var user = new User(
                name,
                email,
                phoneNumber,
                passwordHash,
                accountType);

            user.AddDomainEvent(
                new UserRegisteredEvent(
                    user.Name.DisplayName,
                    user.Email.Value));

            return user;
        }

        public void VerifyEmail()
        {
            if (IsEmailVerified)
                return;

            IsEmailVerified = true;

            Status = UserStatus.Active;

            MarkAsUpdated();

            AddDomainEvent(
                new UserEmailVerifiedEvent(
                    Email.Value));
        }

        public void ChangePassword(string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException(
                    "Password hash is required.",
                    nameof(passwordHash));

            if (PasswordHash == passwordHash)
                return;

            PasswordHash = passwordHash;

            MarkAsUpdated();

            AddDomainEvent(
                new UserPasswordChangedEvent(
                    Email.Value));
        }

        public void UpdateName(FullName name)
        {
            if (name is null)
                throw new ArgumentNullException(nameof(name));

            if (Name == name)
                return;

            Name = name;

            MarkAsUpdated();
        }

        public void UpdatePhoneNumber(PhoneNumber phoneNumber)
        {
            if (phoneNumber is null)
                throw new ArgumentNullException(nameof(phoneNumber));

            if (PhoneNumber == phoneNumber)
                return;

            PhoneNumber = phoneNumber;

            MarkAsUpdated();

            AddDomainEvent(
                new UserPhoneNumberChangedEvent(
                    Email.Value,
                    PhoneNumber.Value));
        }

        public void RecordLogin()
        {
            LastLoginAt = DateTime.UtcNow;

            MarkAsUpdated();
        }

        public void Suspend()
        {
            if (Status == UserStatus.Suspended)
                return;

            Status = UserStatus.Suspended;

            MarkAsUpdated();

            AddDomainEvent(
                new UserSuspendedEvent(
                    Email.Value));
        }

        public void Activate()
        {
            if (!IsEmailVerified)
                throw new InvalidOperationException(
                    "User must verify email before activation.");

            if (Status == UserStatus.Active)
                return;

            Status = UserStatus.Active;

            MarkAsUpdated();

            AddDomainEvent(
                new UserActivatedEvent(
                    Email.Value));
        }

        public void Deactivate()
        {
            if (Status == UserStatus.Inactive)
                return;

            Status = UserStatus.Inactive;

            MarkAsUpdated();

            AddDomainEvent(
                new UserDeactivatedEvent(
                    Email.Value));
        }
    }
}