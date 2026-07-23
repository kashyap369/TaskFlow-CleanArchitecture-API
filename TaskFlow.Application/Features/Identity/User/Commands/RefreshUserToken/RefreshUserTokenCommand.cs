using MediatR;
using TaskFlow.Application.Features.Identity.User.DTOs.Commands.LoginUser;

namespace TaskFlow.Application.Features.Identity.User.Commands.RefreshUserToken
{
    public sealed record RefreshUserTokenCommand(
        string RefreshToken
    ) : IRequest<LoginUserResponseDto>;
}
