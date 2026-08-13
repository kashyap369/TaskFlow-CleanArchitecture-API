using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Features.Identity.User.DTOs.Commands.LoginUser;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Identity.User.Services
{
    public sealed class AuthSessionIssuer : IAuthSessionIssuer
    {
        private readonly IUserRepository _userRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IJwtProvider _jwtProvider;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public AuthSessionIssuer(
            IUserRepository userRepository,
            IUserRoleRepository userRoleRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IJwtProvider jwtProvider,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _userRoleRepository = userRoleRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _jwtProvider = jwtProvider;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task<LoginUserResponseDto> IssueAsync(
            Domain.Entities.Identity.User user,
            CancellationToken cancellationToken = default)
        {
            user.RecordLogin();
            _userRepository.Update(user);

            var roles = await _userRoleRepository
                .GetRoleNamesByUserIdAsync(user.Id, cancellationToken);

            var accessToken = _jwtProvider.GenerateToken(
                user.Id,
                user.Email.Value,
                roles);

            var refreshToken = new Domain.Entities.Identity.RefreshToken(
                user.Id,
                _jwtProvider.GenerateRefreshToken(),
                _jwtProvider.GetRefreshTokenExpiryDate(),
                _currentUserService.IpAddress);

            await _refreshTokenRepository.AddAsync(
                refreshToken,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new LoginUserResponseDto
            {
                UserId = user.Id,
                FullName = user.FullName.DisplayName,
                Email = user.Email.Value,
                Token = accessToken,
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiresAt = refreshToken.ExpiresAt,
                Roles = roles
            };
        }
    }
}
