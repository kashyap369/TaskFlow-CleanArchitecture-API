using FluentValidation;

namespace TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.DeactivateMember
{
    public sealed class DeactivateMemberCommandValidator
        : AbstractValidator<DeactivateMemberCommand>
    {
        public DeactivateMemberCommandValidator()
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