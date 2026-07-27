using MediatR;

namespace TaskFlow.Application.Features.Identity.User.Commands.VerifyEmail
{
    /// <summary>
    /// Completes registration: turns a PendingVerification account into an
    /// Active one so the user can sign in. Anonymous — the token is the proof.
    /// </summary>
    public sealed record VerifyEmailCommand(
        string Token
    ) : IRequest;
}
