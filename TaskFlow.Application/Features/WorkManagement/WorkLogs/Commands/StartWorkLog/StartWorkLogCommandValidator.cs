using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.WorkLogs.Commands.StartWorkLog
{
    public sealed class StartWorkLogCommandValidator
        : AbstractValidator<StartWorkLogCommand>
    {
        public StartWorkLogCommandValidator()
        {
            RuleFor(x => x.TaskId)
                .GreaterThan(0);

            RuleFor(x => x.Notes)
                .MaximumLength(1000);
        }
    }
}
