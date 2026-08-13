using FluentValidation;
using TaskFlow.Application.Features.Identity.User.Commands.RequestLoginCode;

namespace TaskFlow.Application.Features.Identity.User.Validators.Commands.RequestLoginCode
{
    public sealed class RequestLoginCodeCommandValidator
        : AbstractValidator<RequestLoginCodeCommand>
    {
        public RequestLoginCodeCommandValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(256);
        }
    }
}
