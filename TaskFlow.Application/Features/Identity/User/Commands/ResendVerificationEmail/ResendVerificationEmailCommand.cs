using MediatR;

namespace TaskFlow.Application.Features.Identity.User.Commands.ResendVerificationEmail
{
    /// <summary>
    /// Sends a fresh verification link. Anonymous, and deliberately
    /// <b>always succeeds</b> — replying "no such user" would turn this into an
    /// account-enumeration oracle.
    /// </summary>
    public sealed record ResendVerificationEmailCommand(
        string Email
    ) : IRequest;
}
