using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.StartTask
{
    public sealed class StartTaskCommandValidator
        : AbstractValidator<StartTaskCommand>
    {
        public StartTaskCommandValidator()
        {
            RuleFor(x => x.TaskId)
                .GreaterThan(0)
                .WithMessage("Task id is required.");
        }
    }
}