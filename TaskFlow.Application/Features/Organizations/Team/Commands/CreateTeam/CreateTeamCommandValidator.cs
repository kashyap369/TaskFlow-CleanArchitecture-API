using FluentValidation;

namespace TaskFlow.Application.Features.Organizations.Team.Commands.CreateTeam
{
    public sealed class CreateTeamCommandValidator
        : AbstractValidator<CreateTeamCommand>
    {
        public CreateTeamCommandValidator()
        {
            RuleFor(x => x.OrganizationId)
                .GreaterThan(0);

            RuleFor(x => x.Name)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.Description)
                .MaximumLength(500);
        }
    }
}
