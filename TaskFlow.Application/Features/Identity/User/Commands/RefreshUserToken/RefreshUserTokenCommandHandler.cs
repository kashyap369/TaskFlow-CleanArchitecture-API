using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Identity.User.DTOs.Commands.LoginUser;
using TaskFlow.Domain.Enums.Identity;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Identity.User.Commands.RefreshUserToken
{
    public sealed class RefreshUserTokenCommandHandler
        : IRequestHandler<RefreshUserTokenCommand, LoginUserResponseDto>
    {
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly IJwtProvider _jwtProvider;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public RefreshUserTokenCommandHandler(
            IRefreshTokenRepository refreshTokenRepository,
            IUserRepository userRepository,
            IUserRoleRepository userRoleRepository,
            IJwtProvider jwtProvider,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _refreshTokenRepository = refreshTokenRepository;
            _userRepository = userRepository;
            _userRoleRepository = userRoleRepository;
            _jwtProvider = jwtProvider;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task<LoginUserResponseDto> Handle(
            RefreshUserTokenCommand request,
            CancellationToken cancellationToken)
        {
            var existingToken =
                await _refreshTokenRepository.GetByTokenAsync(
                    request.RefreshToken,
                    cancellationToken);

            if (existingToken is null)
            {
                throw new UnauthorizedException(
                    "INVALID_REFRESH_TOKEN",
                    "Refresh token is invalid.");
            }

            // A revoked token being used again means it was
            // probably stolen. Revoke every active token of
            // this user so the attacker is locked out too.
            if (existingToken.IsRevoked)
            {
                var activeTokens =
                    await _refreshTokenRepository
                        .GetActiveByUserIdAsync(
                            existingToken.UserId,
                            cancellationToken);

                foreach (var activeToken in activeTokens)
                {
                    activeToken.Revoke(
                        _currentUserService.IpAddress);

                    _refreshTokenRepository.Update(
                        activeToken);
                }

                await _unitOfWork.SaveChangesAsync(
                    cancellationToken);

                throw new UnauthorizedException(
                    "REFRESH_TOKEN_REUSED",
                    "Refresh token was already used. Please login again.");
            }

            if (existingToken.IsExpired)
            {
                throw new UnauthorizedException(
                    "REFRESH_TOKEN_EXPIRED",
                    "Refresh token has expired. Please login again.");
            }

            var user =
                await _userRepository.GetByIdAsync(
                    existingToken.UserId,
                    cancellationToken);

            if (user is null)
            {
                throw new UnauthorizedException(
                    "INVALID_REFRESH_TOKEN",
                    "Refresh token is invalid.");
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

            var roles =
                await _userRoleRepository
                    .GetRoleNamesByUserIdAsync(
                        user.Id,
                        cancellationToken);

            var token =
                _jwtProvider.GenerateToken(
                    user.Id,
                    user.Email.Value,
                    roles);

            // Rotation: the old refresh token is revoked and
            // replaced by a new one, so each refresh token
            // can only ever be used once.
            var newRefreshToken =
                new Domain.Entities.Identity.RefreshToken(
                    user.Id,
                    _jwtProvider.GenerateRefreshToken(),
                    _jwtProvider.GetRefreshTokenExpiryDate(),
                    _currentUserService.IpAddress);

            existingToken.Revoke(
                _currentUserService.IpAddress,
                newRefreshToken.Token);

            _refreshTokenRepository.Update(
                existingToken);

            await _refreshTokenRepository.AddAsync(
                newRefreshToken,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return new LoginUserResponseDto
            {
                UserId = user.Id,
                FullName = user.FullName.DisplayName,
                Email = user.Email.Value,
                Token = token,
                RefreshToken = newRefreshToken.Token,
                RefreshTokenExpiresAt = newRefreshToken.ExpiresAt,
                Roles = roles
            };
        }
    }
}
