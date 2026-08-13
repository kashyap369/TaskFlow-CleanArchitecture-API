using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Identity.User.Services;
using TaskFlow.Domain.Enums.Identity;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Application.Features.Identity.User.Commands.ResetPassword
{
    public sealed class ResetPasswordCommandHandler
        : IRequestHandler<ResetPasswordCommand>
    {
        private readonly IUserRepository _userRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ICurrentUserService _currentUserService;
        private readonly OneTimeCodeVerifier _verifier;
        private readonly IUnitOfWork _unitOfWork;

        public ResetPasswordCommandHandler(
            IUserRepository userRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IPasswordHasher passwordHasher,
            ICurrentUserService currentUserService,
            OneTimeCodeVerifier verifier,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _passwordHasher = passwordHasher;
            _currentUserService = currentUserService;
            _verifier = verifier;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            ResetPasswordCommand request,
            CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailAsync(
                new Email(request.Email),
                cancellationToken);

            if (user is null)
                throw InvalidCode();

            await _verifier.VerifyAsync(
                user.Id,
                OneTimeCodePurpose.PasswordReset,
                request.Code,
                cancellationToken);

            user.ChangePassword(_passwordHasher.Hash(request.NewPassword));
            _userRepository.Update(user);

            var refreshTokens = await _refreshTokenRepository
                .GetActiveByUserIdAsync(user.Id, cancellationToken);
            foreach (var refreshToken in refreshTokens)
                refreshToken.Revoke(_currentUserService.IpAddress);
            _refreshTokenRepository.UpdateRange(refreshTokens);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private static UnauthorizedException InvalidCode() =>
            new(
                "INVALID_OR_EXPIRED_CODE",
                "The code is invalid or has expired. Request a new code and try again.");
    }
}
