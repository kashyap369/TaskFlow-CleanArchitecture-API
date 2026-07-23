using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.WorkLogs.Commands.LogManualWork
{
    public sealed class LogManualWorkCommandValidator
        : AbstractValidator<LogManualWorkCommand>
    {
        public LogManualWorkCommandValidator()
        {
            RuleFor(x => x.TaskId)
                .GreaterThan(0);

            RuleFor(x => x.StartedAt)
                .NotEmpty();

            RuleFor(x => x.EndedAt)
                .NotEmpty()
                .GreaterThan(x => x.StartedAt)
                .WithMessage("End time must be after start time.");

            RuleFor(x => x.Notes)
                .MaximumLength(1000);
        }
    }
}
