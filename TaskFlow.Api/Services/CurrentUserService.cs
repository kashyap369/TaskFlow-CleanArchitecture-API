using System.Security.Claims;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;

namespace TaskFlow.Api.Services
{
    public sealed class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(
            IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int UserId
        {
            get
            {
                var userIdClaim =
                    _httpContextAccessor.HttpContext?
                        .User
                        .FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrWhiteSpace(userIdClaim))
                {
                    throw new UnauthorizedException(
                        "UNAUTHORIZED",
                        "User is not authenticated.");
                }

                return int.Parse(userIdClaim);
            }
        }

        public string Email
        {
            get
            {
                var emailClaim =
                    _httpContextAccessor.HttpContext?
                        .User
                        .FindFirstValue(ClaimTypes.Email);

                if (string.IsNullOrWhiteSpace(emailClaim))
                {
                    throw new UnauthorizedException(
                        "UNAUTHORIZED",
                        "User is not authenticated.");
                }

                return emailClaim;
            }
        }

        public string IpAddress
        {
            get
            {
                var ipAddress =
                    _httpContextAccessor.HttpContext?
                        .Connection
                        .RemoteIpAddress?
                        .ToString();

                return string.IsNullOrWhiteSpace(ipAddress)
                    ? "unknown"
                    : ipAddress;
            }
        }

        public bool IsAdmin
        {
            get
            {
                // Never throws: an unauthenticated request is simply
                // "not an admin". The [Authorize] layer has already
                // rejected it long before a handler can ask.
                return _httpContextAccessor.HttpContext?
                    .User
                    .IsInRole(SystemRoleNames.Admin)
                    ?? false;
            }
        }
    }
}
