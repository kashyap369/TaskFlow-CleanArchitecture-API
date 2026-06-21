using FluentValidation;

namespace TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.DeleteRole
{
    public sealed class DeleteRoleCommandValidator
        : AbstractValidator<DeleteRoleCommand>
    {
        public DeleteRoleCommandValidator()
        {
            RuleFor(x => x.RoleId)
                .GreaterThan(0)
                .WithMessage("Role id is required.");
        }
    }
}