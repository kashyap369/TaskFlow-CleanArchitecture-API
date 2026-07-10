using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.CreateTask
{
    public sealed class CreateTaskCommandValidator
        : AbstractValidator<CreateTaskCommand>
    {
        public CreateTaskCommandValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty()
                .MaximumLength(200);

            RuleFor(x => x.OrganizationId)
                .GreaterThan(0);

            RuleFor(x => x.ExpectedCompletionDate)
                .GreaterThan(x => x.StartDate)
                .When(x => x.ExpectedCompletionDate.HasValue);
        }
    }
}