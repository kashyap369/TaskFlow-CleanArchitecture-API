using FluentValidation;

namespace TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.ActivateMember
{
    public sealed class ActivateMemberCommandValidator
        : AbstractValidator<ActivateMemberCommand>
    {
        public ActivateMemberCommandValidator()
        {
            RuleFor(x => x.OrganizationId)
                .GreaterThan(0)
                .WithMessage("Organization id is required.");

            RuleFor(x => x.UserId)
                .GreaterThan(0)
                .WithMessage("User id is required.");
        }
    }
}