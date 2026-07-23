using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Identity.User.Commands.LogoutUser
{
    public sealed class LogoutUserCommandHandler
        : IRequestHandler<LogoutUserCommand>
    {
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public LogoutUserCommandHandler(
            IRefreshTokenRepository refreshTokenRepository,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _refreshTokenRepository = refreshTokenRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            LogoutUserCommand request,
            CancellationToken cancellationToken)
        {
            var refreshToken =
                await _refreshTokenRepository.GetByTokenAsync(
                    request.RefreshToken,
                    cancellationToken);

            // Logout never fails: if the token is unknown or
            // already revoked there is nothing left to do.
            if (refreshToken is null || !refreshToken.IsActive)
            {
                return;
            }

            refreshToken.Revoke(
                _currentUserService.IpAddress);

            _refreshTokenRepository.Update(
                refreshToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}
