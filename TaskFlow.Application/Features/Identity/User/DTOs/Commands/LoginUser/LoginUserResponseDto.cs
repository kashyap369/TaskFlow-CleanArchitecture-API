namespace TaskFlow.Application.Features.Identity.User.DTOs.Commands.LoginUser
{
    public sealed class LoginUserResponseDto
    {
        public int UserId { get; init; }

        public string FullName { get; init; } = string.Empty;

        public string Email { get; init; } = string.Empty;

        public string Token { get; init; } = string.Empty;
    }
}