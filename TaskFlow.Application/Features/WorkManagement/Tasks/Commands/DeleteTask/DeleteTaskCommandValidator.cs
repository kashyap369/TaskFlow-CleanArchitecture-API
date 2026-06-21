using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.DeleteTask
{
    public sealed class DeleteTaskCommandValidator
        : AbstractValidator<DeleteTaskCommand>
    {
        public DeleteTaskCommandValidator()
        {
            RuleFor(x => x.TaskId)
                .GreaterThan(0)
                .WithMessage("Task id is required.");
        }
    }
}