using TaskFlow.Application.Features.Identity.User.DTOs.Commands.LoginUser;
using TaskFlow.Domain.Entities.Identity;

namespace TaskFlow.Application.Contracts.Security
{
    public interface IAuthSessionIssuer
    {
        Task<LoginUserResponseDto> IssueAsync(
            User user,
            CancellationToken cancellationToken = default);
    }
}
