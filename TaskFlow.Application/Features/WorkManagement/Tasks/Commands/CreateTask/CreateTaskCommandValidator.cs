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

            // Null = a personal task. When supplied it must be a real id.
            RuleFor(x => x.OrganizationId)
                .GreaterThan(0)
                .When(x => x.OrganizationId.HasValue);

            // Projects only exist inside organizations.
            RuleFor(x => x.ProjectId)
                .Null()
                .When(x => !x.OrganizationId.HasValue)
                .WithMessage(
                    "A personal task cannot belong to a project.");

            // Teams only exist inside organizations.
            RuleFor(x => x.TeamId)
                .Null()
                .When(x => !x.OrganizationId.HasValue)
                .WithMessage(
                    "A personal task cannot belong to a team.");

            RuleFor(x => x.TeamId)
                .GreaterThan(0)
                .When(x => x.TeamId.HasValue);

            RuleFor(x => x.ExpectedCompletionDate)
                .GreaterThan(x => x.StartDate)
                .When(x => x.ExpectedCompletionDate.HasValue);
        }
    }
}