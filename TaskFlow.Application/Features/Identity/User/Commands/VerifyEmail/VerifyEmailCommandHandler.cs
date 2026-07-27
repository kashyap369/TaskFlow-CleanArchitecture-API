using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Identity.User.Commands.VerifyEmail
{
    public sealed class VerifyEmailCommandHandler
        : IRequestHandler<VerifyEmailCommand>
    {
        private readonly IUserRepository _userRepository;
        private readonly IEmailVerificationTokenService _tokenService;
        private readonly IUnitOfWork _unitOfWork;

        public VerifyEmailCommandHandler(
            IUserRepository userRepository,
            IEmailVerificationTokenService tokenService,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            VerifyEmailCommand request,
            CancellationToken cancellationToken)
        {
            if (!_tokenService.TryValidate(
                    request.Token,
                    out var userId))
            {
                throw new BusinessException(
                    "INVALID_VERIFICATION_TOKEN",
                    "This verification link is invalid or has expired. " +
                    "Request a new one.");
            }

            var user =
                await _userRepository.GetByIdAsync(
                    userId,
                    cancellationToken);

            if (user is null)
            {
                throw new NotFoundException(
                    "USER_NOT_FOUND",
                    "User not found.");
            }

            // Idempotent: VerifyEmail() returns early if already verified, so
            // clicking the link twice succeeds rather than erroring.
            user.VerifyEmail();

            _userRepository.Update(user);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}
