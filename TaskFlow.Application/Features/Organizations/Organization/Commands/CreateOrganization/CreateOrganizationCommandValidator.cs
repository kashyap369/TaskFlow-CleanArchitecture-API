using FluentValidation;

namespace TaskFlow.Application.Features.Organizations.Organization.Commands.CreateOrganization
{
    public sealed class CreateOrganizationCommandValidator
        : AbstractValidator<CreateOrganizationCommand>
    {
        public CreateOrganizationCommandValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Organization name is required.")
                .MaximumLength(200)
                .WithMessage("Organization name cannot exceed 200 characters.");

            RuleFor(x => x.Description)
                .MaximumLength(1000)
                .WithMessage("Description cannot exceed 1000 characters.");

            RuleFor(x => x.OwnerUserId)
                .GreaterThan(0)
                .WithMessage("Owner user id is required.");
        }
    }
}