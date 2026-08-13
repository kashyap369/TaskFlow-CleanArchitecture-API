using MediatR;

namespace TaskFlow.Application.Features.Identity.User.Commands.ResetPassword
{
    public sealed record ResetPasswordCommand(
        string Email,
        string Code,
        string NewPassword) : IRequest;
}
