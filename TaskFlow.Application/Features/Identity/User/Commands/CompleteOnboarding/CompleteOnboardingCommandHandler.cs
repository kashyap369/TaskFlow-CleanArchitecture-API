using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Identity.User.Commands.CompleteOnboarding
{
    public sealed class CompleteOnboardingCommandHandler
        : IRequestHandler<CompleteOnboardingCommand, Unit>
    {
        private readonly IUserRepository _userRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public CompleteOnboardingCommandHandler(
            IUserRepository userRepository,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task<Unit> Handle(
            CompleteOnboardingCommand request,
            CancellationToken cancellationToken)
        {
            var user =
                await _userRepository.GetByIdAsync(
                    _currentUserService.UserId,
                    cancellationToken);

            if (user is null)
            {
                throw new NotFoundException(
                    "USER_NOT_FOUND",
                    "User not found.");
            }

            // Idempotent in the entity, so a second tab posting the same
            // thing is a no-op rather than a conflict.
            user.CompleteOnboarding();

            _userRepository.Update(user);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
