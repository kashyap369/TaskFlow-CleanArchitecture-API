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
