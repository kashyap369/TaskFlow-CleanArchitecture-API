using Microsoft.Extensions.Logging;
using TaskFlow.Application.Contracts.Configuration;
using TaskFlow.Application.Contracts.Email;
using TaskFlow.Domain.DomainEvents.Identity.User;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Application.DomainEvents.Identity.User
{
    /// <summary>
    /// Sends the thank-you once a new account is actually usable.
    /// <para>
    /// It is deliberately tied to <b>verification</b> rather than to
    /// registration. Two reasons: at registration the only thing the user
    /// needs is the verify link, and a second message alongside it competes
    /// with the one that matters; and verification is the point at which the
    /// address is known to be real, so nothing is sent into a typo. On a
    /// domain with no sending history that second point is not cosmetic —
    /// mail to addresses that bounce is what earns a spam reputation.
    /// </para>
    /// <para>
    /// This goes out as <see cref="EmailSender.Product"/>, so it is the one
    /// message in the system a user can usefully reply to.
    /// </para>
    /// </summary>
    public sealed class UserEmailVerifiedEventHandler
        : IDomainEventHandler<UserEmailVerifiedEvent>
    {
        private readonly IEmailService _emailService;
        private readonly IUserRepository _userRepository;
        private readonly IClientUrlProvider _clientUrlProvider;
        private readonly ILogger<UserEmailVerifiedEventHandler> _logger;

        public UserEmailVerifiedEventHandler(
            IEmailService emailService,
            IUserRepository userRepository,
            IClientUrlProvider clientUrlProvider,
            ILogger<UserEmailVerifiedEventHandler> logger)
        {
            _emailService = emailService;
            _userRepository = userRepository;
            _clientUrlProvider = clientUrlProvider;
            _logger = logger;
        }

        public async Task HandleAsync(
            UserEmailVerifiedEvent domainEvent,
            CancellationToken cancellationToken)
        {
            // The event carries only the address, so the display name has to
            // be resolved. A missing user is not an error worth failing on —
            // the greeting simply falls back.
            var user =
                await _userRepository.GetByEmailAsync(
                    new Email(domainEvent.Email),
                    cancellationToken);

            var userName =
                user is null
                    ? "there"
                    : user.FullName.ToString();

            var templatePath = Path.Combine(
                AppContext.BaseDirectory,
                "Email",
                "Templates",
                "ThankYou.html");

            try
            {
                var template =
                    await File.ReadAllTextAsync(
                        templatePath,
                        cancellationToken);

                template = template
                    .Replace(
                        "{{UserName}}",
                        userName)
                    .Replace(
                        "{{Email}}",
                        domainEvent.Email)
                    .Replace(
                        "{{LoginUrl}}",
                        $"{_clientUrlProvider.BaseUrl}/auth/login")
                    .Replace(
                        "{{CurrentYear}}",
                        DateTime.UtcNow.Year.ToString());

                await _emailService.SendAsync(
                    domainEvent.Email,
                    "Welcome to TaskFlow",
                    template,
                    EmailSender.Product,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Verification has already been committed by the time domain
                // events are dispatched. A courtesy email must never be the
                // reason a user who clicked a valid link is told it failed —
                // they would be left unable to sign in over a thank-you.
                _logger.LogError(
                    exception,
                    "Failed to send the thank-you email after verification. "
                    + "The account is verified and usable; only the courtesy "
                    + "message was lost.");
            }
        }
    }
}
