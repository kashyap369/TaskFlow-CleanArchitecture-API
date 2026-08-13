using TaskFlow.Application.Contracts.Configuration;
using TaskFlow.Application.Contracts.Email;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Domain.DomainEvents.Identity.User;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Application.DomainEvents.Identity.User
{
    /// <summary>
    /// Sends the welcome email — which is also the <b>verification</b> email.
    /// A new account is PendingVerification and cannot sign in until the link
    /// in this mail is opened, so this is the only way in for a real user.
    /// </summary>
    public sealed class UserRegisteredEventHandler
        : IDomainEventHandler<UserRegisteredEvent>
    {
        /// <summary>
        /// Where the Angular client is served from. Matches the API's CORS
        /// origin (http, not https — the dev server is plain HTTP).
        /// </summary>
        private readonly IEmailService _emailService;
        private readonly IUserRepository _userRepository;
        private readonly IEmailVerificationTokenService _tokenService;
        private readonly IClientUrlProvider _clientUrlProvider;

        public UserRegisteredEventHandler(
            IEmailService emailService,
            IUserRepository userRepository,
            IEmailVerificationTokenService tokenService,
            IClientUrlProvider clientUrlProvider)
        {
            _emailService = emailService;
            _userRepository = userRepository;
            _tokenService = tokenService;
            _clientUrlProvider = clientUrlProvider;
        }

        public async Task HandleAsync(
            UserRegisteredEvent domainEvent,
            CancellationToken cancellationToken)
        {
            // UserRegisteredEvent is raised inside Register(), before the row
            // exists, so it cannot carry the id — resolve it by email now that
            // the save has happened.
            var user =
                await _userRepository.GetByEmailAsync(
                    new Email(domainEvent.Email),
                    cancellationToken);

            var verifyUrl =
                user is null
                    ? $"{_clientUrlProvider.BaseUrl}/auth/login"
                    : $"{_clientUrlProvider.BaseUrl}/auth/verify-email?token="
                      + Uri.EscapeDataString(
                          _tokenService.Generate(user.Id));

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
                    "{{VerifyUrl}}",
                    verifyUrl)
                // The template's existing CTA points at {{LoginUrl}}; until a
                // user is verified, "log in" IS "verify", so both resolve here.
                .Replace(
                    "{{LoginUrl}}",
                    verifyUrl)
                .Replace(
                    "{{CurrentYear}}",
                    DateTime.UtcNow.Year.ToString());

            await _emailService.SendAsync(
                domainEvent.Email,
                "Verify your TaskFlow account",
                template,
                cancellationToken);
        }
    }
}
