using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.CompleteSubTask
{
    public sealed class CompleteSubTaskCommandValidator
        : AbstractValidator<CompleteSubTaskCommand>
    {
        public CompleteSubTaskCommandValidator()
        {
            RuleFor(x => x.SubTaskId)
                .GreaterThan(0)
                .WithMessage("SubTask id is required.");
        }
    }
}