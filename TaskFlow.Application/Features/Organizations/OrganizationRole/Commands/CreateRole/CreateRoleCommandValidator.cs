using FluentValidation;

namespace TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.CreateRole
{
    public sealed class CreateRoleCommandValidator
        : AbstractValidator<CreateRoleCommand>
    {
        public CreateRoleCommandValidator()
        {
            RuleFor(x => x.OrganizationId)
                .GreaterThan(0)
                .WithMessage("Organization id is required.");

            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Role name is required.")
                .MaximumLength(100)
                .WithMessage("Role name cannot exceed 100 characters.");

            RuleFor(x => x.Description)
                .MaximumLength(500)
                .WithMessage("Description cannot exceed 500 characters.");
        }
    }
}