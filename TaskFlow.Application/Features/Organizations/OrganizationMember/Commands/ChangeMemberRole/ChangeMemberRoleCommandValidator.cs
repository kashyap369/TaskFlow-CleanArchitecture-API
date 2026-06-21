using FluentValidation;

namespace TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.ChangeMemberRole
{
    public sealed class ChangeMemberRoleCommandValidator
        : AbstractValidator<ChangeMemberRoleCommand>
    {
        public ChangeMemberRoleCommandValidator()
        {
            RuleFor(x => x.OrganizationId)
                .GreaterThan(0)
                .WithMessage("Organization id is required.");

            RuleFor(x => x.UserId)
                .GreaterThan(0)
                .WithMessage("User id is required.");

            RuleFor(x => x.OrganizationRoleId)
                .GreaterThan(0)
                .WithMessage("Organization role id is required.");
        }
    }
}