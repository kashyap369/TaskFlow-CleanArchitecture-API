using FluentValidation;
using TaskFlow.Application.Features.Identity.User.Commands.RequestPasswordReset;

namespace TaskFlow.Application.Features.Identity.User.Validators.Commands.RequestPasswordReset
{
    public sealed class RequestPasswordResetCommandValidator
        : AbstractValidator<RequestPasswordResetCommand>
    {
        public RequestPasswordResetCommandValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(256);
        }
    }
}
