using FluentValidation;

namespace TaskFlow.Application.Features.Organizations.Organization.Commands.DeleteOrganization
{
    public sealed class DeleteOrganizationCommandValidator
        : AbstractValidator<DeleteOrganizationCommand>
    {
        public DeleteOrganizationCommandValidator()
        {
            RuleFor(x => x.OrganizationId)
                .GreaterThan(0)
                .WithMessage("Organization id is required.");
        }
    }
}