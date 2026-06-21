using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.CompleteTask
{
    public sealed class CompleteTaskCommandValidator
        : AbstractValidator<CompleteTaskCommand>
    {
        public CompleteTaskCommandValidator()
        {
            RuleFor(x => x.TaskId)
                .GreaterThan(0)
                .WithMessage("Task id is required.");
        }
    }
}