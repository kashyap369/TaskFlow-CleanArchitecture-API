using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.DeleteSubTask
{
    public sealed class DeleteSubTaskCommandValidator
        : AbstractValidator<DeleteSubTaskCommand>
    {
        public DeleteSubTaskCommandValidator()
        {
            RuleFor(x => x.SubTaskId)
                .GreaterThan(0)
                .WithMessage("SubTask id is required.");
        }
    }
}