using MediatR;

namespace TaskFlow.Application.Features.Identity.User.Commands.RequestLoginCode
{
    public sealed record RequestLoginCodeCommand(string Email) : IRequest;
}
