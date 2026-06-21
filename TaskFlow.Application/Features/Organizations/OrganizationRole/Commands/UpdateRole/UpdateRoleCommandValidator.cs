using FluentValidation;

namespace TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.UpdateRole
{
    public sealed class UpdateRoleCommandValidator
        : AbstractValidator<UpdateRoleCommand>
    {
        public UpdateRoleCommandValidator()
        {
            RuleFor(x => x.RoleId)
                .GreaterThan(0)
                .WithMessage("Role id is required.");

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