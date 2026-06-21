using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.UpdateSubTask
{
    public sealed class UpdateSubTaskCommandValidator
        : AbstractValidator<UpdateSubTaskCommand>
    {
        public UpdateSubTaskCommandValidator()
        {
            RuleFor(x => x.SubTaskId)
                .GreaterThan(0)
                .WithMessage("SubTask id is required.");

            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("SubTask title is required.")
                .MaximumLength(200)
                .WithMessage(
                    "SubTask title cannot exceed 200 characters.");
        }
    }
}