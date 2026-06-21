using FluentValidation;

namespace TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.RemoveMember
{
    public sealed class RemoveMemberCommandValidator
        : AbstractValidator<RemoveMemberCommand>
    {
        public RemoveMemberCommandValidator()
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