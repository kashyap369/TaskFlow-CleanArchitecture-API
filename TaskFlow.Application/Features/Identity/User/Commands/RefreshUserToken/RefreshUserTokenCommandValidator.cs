using FluentValidation;

namespace TaskFlow.Application.Features.Identity.User.Commands.RefreshUserToken
{
    public sealed class RefreshUserTokenCommandValidator
        : AbstractValidator<RefreshUserTokenCommand>
    {
        public RefreshUserTokenCommandValidator()
        {
            RuleFor(x => x.RefreshToken)
                .NotEmpty();
        }
    }
}
