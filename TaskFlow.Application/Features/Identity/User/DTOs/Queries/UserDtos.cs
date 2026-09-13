using TaskFlow.Domain.Enums.Identity;

namespace TaskFlow.Application.Features.Identity.User.DTOs.Queries
{
    public sealed class UserDetailDto
    {
        public int Id { get; init; }
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string PhoneNumber { get; init; } = string.Empty;
        public UserStatus Status { get; init; }
        public AccountType AccountType { get; init; }
        public bool IsEmailVerified { get; init; }
        public DateTime? LastLoginAt { get; init; }
        public DateTime CreatedAt { get; init; }

        /// <summary>
        /// False only for an account that has never been through the
        /// first-run welcome. The client uses it to decide whether to
        /// play that welcome, so it must stay on this payload.
        /// </summary>
        public bool HasCompletedOnboarding { get; init; }
    }

    public sealed class UserListItemDto
    {
        public int Id { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public UserStatus Status { get; init; }
        public AccountType AccountType { get; init; }
    }
}
