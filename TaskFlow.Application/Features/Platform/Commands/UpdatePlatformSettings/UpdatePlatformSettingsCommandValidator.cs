using FluentValidation;

namespace TaskFlow.Application.Features.Platform.Commands.UpdatePlatformSettings
{
    public sealed class UpdatePlatformSettingsCommandValidator
        : AbstractValidator<UpdatePlatformSettingsCommand>
    {
        public UpdatePlatformSettingsCommandValidator()
        {
            RuleFor(x => x.ApplicationName)
                .NotEmpty()
                .MaximumLength(200);

            RuleFor(x => x.SupportEmail)
                .EmailAddress()
                .MaximumLength(256)
                .When(x => !string.IsNullOrWhiteSpace(x.SupportEmail));

            RuleFor(x => x.MaintenanceMessage)
                .MaximumLength(1000);
        }
    }
}
