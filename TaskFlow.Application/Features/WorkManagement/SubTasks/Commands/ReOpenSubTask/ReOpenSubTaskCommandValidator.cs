using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.ReOpenSubTask
{
    public sealed class ReOpenSubTaskCommandValidator
        : AbstractValidator<ReOpenSubTaskCommand>
    {
        public ReOpenSubTaskCommandValidator()
        {
            RuleFor(x => x.SubTaskId)
                .GreaterThan(0)
                .WithMessage("SubTask id is required.");
        }
    }
}