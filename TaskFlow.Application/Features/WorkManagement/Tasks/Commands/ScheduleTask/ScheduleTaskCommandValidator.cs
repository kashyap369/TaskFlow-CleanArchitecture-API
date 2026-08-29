using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.ScheduleTask;

public sealed class ScheduleTaskCommandValidator
    : AbstractValidator<ScheduleTaskCommand>
{
    public ScheduleTaskCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .GreaterThan(0)
            .WithMessage("Task id is required.");

        RuleFor(x => x.StartDate)
            .NotEqual(default(DateTime))
            .WithMessage("Task start date is required.");

        RuleFor(x => x.ExpectedCompletionDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.ExpectedCompletionDate.HasValue)
            .WithMessage("Expected completion date cannot be before the start date.");
    }
}
