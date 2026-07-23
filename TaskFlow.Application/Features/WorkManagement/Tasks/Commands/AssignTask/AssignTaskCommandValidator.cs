using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.AssignTask
{
    public sealed class AssignTaskCommandValidator
        : AbstractValidator<AssignTaskCommand>
    {
        public AssignTaskCommandValidator()
        {
            RuleFor(x => x.TaskId)
                .GreaterThan(0);

            RuleFor(x => x.AssignedToUserId)
                .GreaterThan(0);
        }
    }
}
