using FluentValidation;

namespace TaskFlow.Application.Features.Organizations.Organization.Commands.UpdateOraganization
{
    public sealed class UpdateOrganizationCommandValidator
        : AbstractValidator<UpdateOrganizationCommand>
    {
        public UpdateOrganizationCommandValidator()
        {
            RuleFor(x => x.OrganizationId)
                .GreaterThan(0)
                .WithMessage("Organization id is required.");

            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Organization name is required.")
                .MaximumLength(200)
                .WithMessage(
                    "Organization name cannot exceed 200 characters.");

            RuleFor(x => x.Description)
                .MaximumLength(1000)
                .WithMessage(
                    "Description cannot exceed 1000 characters.");
        }
    }
}