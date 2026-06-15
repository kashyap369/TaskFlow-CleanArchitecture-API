using TaskFlow.Application.Contracts.Email;
using TaskFlow.Domain.DomainEvents.Identity.User;

namespace TaskFlow.Application.DomainEvents.Identity.User
{
    public sealed class UserRegisteredEventHandler
        : IDomainEventHandler<UserRegisteredEvent>
    {
        private readonly IEmailService _emailService;

        public UserRegisteredEventHandler(
            IEmailService emailService)
        {
            _emailService = emailService;
        }

        public async Task HandleAsync(
            UserRegisteredEvent domainEvent,
            CancellationToken cancellationToken)
        {
            var templatePath = Path.Combine(
                AppContext.BaseDirectory,
                "Email",
                "Templates",
                "Welcome.html");

            var template =
                await File.ReadAllTextAsync(
                    templatePath,
                    cancellationToken);

            template = template
                .Replace(
                    "{{UserName}}",
                    domainEvent.FullName)
                .Replace(
                    "{{Email}}",
                    domainEvent.Email)
                .Replace(
                    "{{LoginUrl}}",
                    "https://localhost:4200/login")
                .Replace(
                    "{{CurrentYear}}",
                    DateTime.UtcNow.Year.ToString());

            await _emailService.SendAsync(
                domainEvent.Email,
                "Welcome To TaskFlow",
                template,
                cancellationToken);
        }
    }
}