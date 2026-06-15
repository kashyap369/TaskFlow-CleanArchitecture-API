using MediatR;
using TaskFlow.Application.Features.Identity.User.DTOs.Commands.LoginUser;

namespace TaskFlow.Application.Features.Identity.User.Commands.LoginUser
{
    public sealed record LoginUserCommand(
        string Email,
        string Password)
        : IRequest<LoginUserResponseDto>;
}