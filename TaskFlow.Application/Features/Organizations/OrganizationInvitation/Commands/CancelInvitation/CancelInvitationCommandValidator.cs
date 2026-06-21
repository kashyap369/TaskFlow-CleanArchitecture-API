using FluentValidation;

namespace TaskFlow.Application.Features.Organizations.OrganizationInvitation.Commands.CancelInvitation
{
    public sealed class CancelInvitationCommandValidator
        : AbstractValidator<CancelInvitationCommand>
    {
        public CancelInvitationCommandValidator()
        {
            RuleFor(x => x.InvitationId)
                .GreaterThan(0)
                .WithMessage("Invitation id is required.");
        }
    }
}