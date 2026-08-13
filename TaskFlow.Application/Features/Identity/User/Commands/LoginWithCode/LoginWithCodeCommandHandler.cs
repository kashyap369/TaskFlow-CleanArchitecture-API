using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Identity.User.DTOs.Commands.LoginUser;
using TaskFlow.Application.Features.Identity.User.Services;
using TaskFlow.Domain.Enums.Identity;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Application.Features.Identity.User.Commands.LoginWithCode
{
    public sealed class LoginWithCodeCommandHandler
        : IRequestHandler<LoginWithCodeCommand, LoginUserResponseDto>
    {
        private readonly IUserRepository _userRepository;
        private readonly OneTimeCodeVerifier _verifier;
        private readonly IAuthSessionIssuer _sessionIssuer;

        public LoginWithCodeCommandHandler(
            IUserRepository userRepository,
            OneTimeCodeVerifier verifier,
            IAuthSessionIssuer sessionIssuer)
        {
            _userRepository = userRepository;
            _verifier = verifier;
            _sessionIssuer = sessionIssuer;
        }

        public async Task<LoginUserResponseDto> Handle(
            LoginWithCodeCommand request,
            CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailAsync(
                new Email(request.Email),
                cancellationToken);

            if (user is null)
                throw InvalidCode();

            await _verifier.VerifyAsync(
                user.Id,
                OneTimeCodePurpose.PasswordlessLogin,
                request.Code,
                cancellationToken);

            if (!user.IsEmailVerified || user.Status == UserStatus.PendingVerification)
                throw new UnauthorizedException(
                    "EMAIL_NOT_VERIFIED",
                    "Please verify your email before logging in.");
            if (user.Status == UserStatus.Suspended)
                throw new ForbiddenException(
                    "ACCOUNT_SUSPENDED",
                    "Your account has been suspended.");
            if (user.Status == UserStatus.Inactive)
                throw new ForbiddenException(
                    "ACCOUNT_INACTIVE",
                    "Your account is inactive.");

            return await _sessionIssuer.IssueAsync(user, cancellationToken);
        }

        private static UnauthorizedException InvalidCode() =>
            new(
                "INVALID_OR_EXPIRED_CODE",
                "The code is invalid or has expired. Request a new code and try again.");
    }
}
