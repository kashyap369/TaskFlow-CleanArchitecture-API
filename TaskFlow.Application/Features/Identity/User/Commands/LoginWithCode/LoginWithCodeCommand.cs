using MediatR;
using TaskFlow.Application.Features.Identity.User.DTOs.Commands.LoginUser;

namespace TaskFlow.Application.Features.Identity.User.Commands.LoginWithCode
{
    public sealed record LoginWithCodeCommand(
        string Email,
        string Code) : IRequest<LoginUserResponseDto>;
}
