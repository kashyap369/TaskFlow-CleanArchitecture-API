using System.Security.Claims;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;

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
    }
}
