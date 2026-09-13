using MediatR;

namespace TaskFlow.Application.Features.Identity.User.Commands.CompleteOnboarding
{
    /// <summary>
    /// Marks the signed-in account as having been through the first-run
    /// welcome. Takes no user id on purpose — the account is the one on
    /// the JWT, never one named by the caller.
    /// </summary>
    public sealed record CompleteOnboardingCommand
        : IRequest<Unit>;
}
