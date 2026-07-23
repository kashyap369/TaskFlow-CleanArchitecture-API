using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Identity.User.DTOs.Commands.LoginUser;
using TaskFlow.Domain.Enums.Identity;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Application.Features.Identity.User.Commands.LoginUser
{
    public sealed class LoginUserCommandHandler
        : IRequestHandler<LoginUserCommand, LoginUserResponseDto>
    {
        private readonly IUserRepository _userRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtProvider _jwtProvider;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public LoginUserCommandHandler(
            IUserRepository userRepository,
            IUserRoleRepository userRoleRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IPasswordHasher passwordHasher,
            IJwtProvider jwtProvider,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _userRoleRepository = userRoleRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _passwordHasher = passwordHasher;
            _jwtProvider = jwtProvider;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task<LoginUserResponseDto> Handle(
            LoginUserCommand request,
            CancellationToken cancellationToken)
        {
            var email = new Email(
                request.Email);

            var user =
                await _userRepository.GetByEmailAsync(
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

            _userRepository.Update(user);

            // The user's system roles go into the JWT as
            // role claims, so authorization policies can
            // check them without hitting the database.
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

            // The refresh token is a random string stored in
            // the database. It is used to get a new access
            // token when the current one expires.
            var refreshToken =
                new Domain.Entities.Identity.RefreshToken(
                    user.Id,
                    _jwtProvider.GenerateRefreshToken(),
                    _jwtProvider.GetRefreshTokenExpiryDate(),
                    _currentUserService.IpAddress);

            await _refreshTokenRepository.AddAsync(
                refreshToken,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return new LoginUserResponseDto
            {
                UserId = user.Id,
                FullName = user.FullName.DisplayName,
                Email = user.Email.Value,
                Token = token,
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiresAt = refreshToken.ExpiresAt,
                Roles = roles
            };
        }
    }
}