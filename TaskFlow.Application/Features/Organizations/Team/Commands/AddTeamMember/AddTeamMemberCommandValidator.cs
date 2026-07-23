using FluentValidation;

namespace TaskFlow.Application.Features.Organizations.Team.Commands.AddTeamMember
{
    public sealed class AddTeamMemberCommandValidator
        : AbstractValidator<AddTeamMemberCommand>
    {
        public AddTeamMemberCommandValidator()
        {
            RuleFor(x => x.TeamId)
                .GreaterThan(0);

            RuleFor(x => x.UserId)
                .GreaterThan(0);
        }
    }
}
