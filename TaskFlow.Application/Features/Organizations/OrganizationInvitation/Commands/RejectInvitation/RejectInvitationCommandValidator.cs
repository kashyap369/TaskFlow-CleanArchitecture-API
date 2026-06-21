using FluentValidation;

namespace TaskFlow.Application.Features.Organizations.OrganizationInvitation.Commands.RejectInvitation
{
    public sealed class RejectInvitationCommandValidator
        : AbstractValidator<RejectInvitationCommand>
    {
        public RejectInvitationCommandValidator()
        {
            RuleFor(x => x.InvitationId)
                .GreaterThan(0)
                .WithMessage("Invitation id is required.");
        }
    }
}