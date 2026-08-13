using MediatR;

namespace TaskFlow.Application.Features.Identity.User.Commands.RequestPasswordReset
{
    public sealed record RequestPasswordResetCommand(string Email) : IRequest;
}
