using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Identity.User.DTOs.Commands.LoginUser;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Application.Features.Identity.User.Commands.LoginUser
{
    public sealed class LoginUserCommandHandler
        : IRequestHandler<LoginUserCommand, LoginUserResponseDto>
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtProvider _jwtProvider;

        public LoginUserCommandHandler(
            IUserRepository userRepository,
            IPasswordHasher passwordHasher,
            IJwtProvider jwtProvider)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _jwtProvider = jwtProvider;
        }

        public async Task<LoginUserResponseDto> Handle(
            LoginUserCommand request,
            CancellationToken cancellationToken)
        {
            var email = new Email(request.Email);

            var user = await _userRepository.GetByEmailAsync(
                email,
                cancellationToken);

            if (user is null)
            {
                throw new UnauthorizedException(
                    "INVALID_CREDENTIALS",
                    "Invalid email or password.");
            }

            var isValidPassword =
                _passwordHasher.Verify(
                    request.Password,
                    user.PasswordHash);

            if (!isValidPassword)
            {
                throw new UnauthorizedException(
                    "INVALID_CREDENTIALS",
                    "Invalid email or password.");
            }

            if (user.Status == UserStatus.PendingVerification)
            {
                throw new UnauthorizedException(
                    "EMAIL_NOT_VERIFIED",
                    "Please verify your email before logging in.");
            }

            if (user.Status == UserStatus.Suspended)
            {
                throw new ForbiddenException(
                    "ACCOUNT_SUSPENDED",
                    "Your account has been suspended.");
            }

            if (user.Status == UserStatus.Inactive)
            {
                throw new ForbiddenException(
                    "ACCOUNT_INACTIVE",
                    "Your account is inactive.");
            }

            user.RecordLogin();

            var token = _jwtProvider.GenerateToken(
                user.Id,
                user.Email.Value);

            return new LoginUserResponseDto
            {
                UserId = user.Id,
                FullName = user.Name.DisplayName,
                Email = user.Email.Value,
                Token = token
            };
        }
    }
}