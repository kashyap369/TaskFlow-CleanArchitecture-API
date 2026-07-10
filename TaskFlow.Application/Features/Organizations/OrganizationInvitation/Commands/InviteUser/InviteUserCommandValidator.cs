using FluentValidation;

namespace TaskFlow.Application.Features.Organizations.OrganizationInvitation.Commands.InviteUser
{
    public sealed class InviteUserCommandValidator
        : AbstractValidator<InviteUserCommand>
    {
        public InviteUserCommandValidator()
        {
            RuleFor(x => x.OrganizationId)
                .GreaterThan(0);

            RuleFor(x => x.OrganizationRoleId)
                .GreaterThan(0);

            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(200);
        }
    }
}