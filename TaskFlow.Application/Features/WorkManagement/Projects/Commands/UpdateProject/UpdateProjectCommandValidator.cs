using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Commands.UpdateProject
{
    public sealed class UpdateProjectCommandValidator
        : AbstractValidator<UpdateProjectCommand>
    {
        public UpdateProjectCommandValidator()
        {
            RuleFor(x => x.ProjectId)
                .GreaterThan(0)
                .WithMessage("Project id is required.");

            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Project title is required.")
                .MaximumLength(200)
                .WithMessage(
                    "Project title cannot exceed 200 characters.");

            RuleFor(x => x.Description)
                .MaximumLength(2000)
                .WithMessage(
                    "Description cannot exceed 2000 characters.");
        }
    }
}