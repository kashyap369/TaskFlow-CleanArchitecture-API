using FluentValidation;

namespace TaskFlow.Application.Features.Organizations.OrganizationInvitation.Commands.AcceptInvitation
{
    public sealed class AcceptInvitationCommandValidator
        : AbstractValidator<AcceptInvitationCommand>
    {
        public AcceptInvitationCommandValidator()
        {
            RuleFor(x => x.InvitationId)
                .GreaterThan(0)
                .WithMessage("Invitation id is required.");
        }
    }
}