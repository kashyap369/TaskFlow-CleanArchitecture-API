using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Commands.DeleteProject
{
    public sealed class DeleteProjectCommandValidator
        : AbstractValidator<DeleteProjectCommand>
    {
        public DeleteProjectCommandValidator()
        {
            RuleFor(x => x.ProjectId)
                .GreaterThan(0)
                .WithMessage("Project id is required.");
        }
    }
}