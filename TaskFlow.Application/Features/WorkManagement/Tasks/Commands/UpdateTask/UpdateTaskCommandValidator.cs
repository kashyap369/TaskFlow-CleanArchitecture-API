using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.UpdateTask
{
    public sealed class UpdateTaskCommandValidator
        : AbstractValidator<UpdateTaskCommand>
    {
        public UpdateTaskCommandValidator()
        {
            RuleFor(x => x.TaskId)
                .GreaterThan(0)
                .WithMessage("Task id is required.");

            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Task title is required.")
                .MaximumLength(200)
                .WithMessage(
                    "Task title cannot exceed 200 characters.");

            RuleFor(x => x.Description)
                .MaximumLength(2000)
                .WithMessage(
                    "Description cannot exceed 2000 characters.");
        }
    }
}