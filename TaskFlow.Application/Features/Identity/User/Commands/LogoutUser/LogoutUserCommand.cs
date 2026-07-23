using MediatR;

namespace TaskFlow.Application.Features.Identity.User.Commands.LogoutUser
{
    public sealed record LogoutUserCommand(
        string RefreshToken
    ) : IRequest;
}
