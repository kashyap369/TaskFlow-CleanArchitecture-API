using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.CreateSubTask
{
    public sealed class CreateSubTaskCommandValidator
        : AbstractValidator<CreateSubTaskCommand>
    {
        public CreateSubTaskCommandValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("SubTask title is required.")
                .MaximumLength(200)
                .WithMessage(
                    "SubTask title cannot exceed 200 characters.");

            RuleFor(x => x.TaskId)
                .GreaterThan(0)
                .WithMessage("Task id is required.");
        }
    }
}