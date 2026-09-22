using TaskFlow.Application.Contracts.Email;

namespace TaskFlow.Infra.Email
{
    /// <summary>
    /// One SMTP server (self-hosted mailcow), two sending identities.
    /// <para>
    /// <b>Host is not the From domain.</b> mailcow serves both
    /// <c>buildbykashyap.in</c> and <c>inksphere.space</c> from one box, and
    /// <c>mail.inksphere.space</c> has no A record — so <see cref="Host"/> is
    /// <c>mail.buildbykashyap.in</c> while the addresses are
    /// <c>@inksphere.space</c>. Matching the host to the address domain is the
    /// intuitive change and takes all outbound mail down.
    /// </para>
    /// </summary>
    public sealed class EmailSettings
    {
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// 587 (STARTTLS). <b>Not 465.</b> The sender uses
        /// <c>System.Net.Mail.SmtpClient</c>, which has no implicit-TLS mode —
        /// its <see cref="EnableSsl"/> means STARTTLS. Port 465 is open on the
        /// host and looks like the safer choice, but the client cannot speak
        /// it and the connection hangs instead of failing.
        /// </summary>
        public int Port { get; set; }

        public bool EnableSsl { get; set; }

        /// <summary>
        /// Credentials used when a sender does not carry its own. One mailbox
        /// may send as the other when mailcow's "Allow to send as" permits it,
        /// which keeps the deployment down to a single app password.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// A mailbox <b>app password</b> scoped to SMTP — never the mailbox
        /// login password. Supplied from Infisical; never committed.
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary><c>noreply@</c> — see <see cref="EmailSender.Transactional"/>.</summary>
        public EmailSenderSettings Transactional { get; set; } = new();

        /// <summary><c>taskflow@</c> — see <see cref="EmailSender.Product"/>.</summary>
        public EmailSenderSettings Product { get; set; } = new();

        /// <summary>
        /// Resolves one sending identity, filling in any credential the sender
        /// does not define from the shared pair above.
        /// </summary>
        public EmailSenderSettings For(EmailSender sender)
        {
            var settings =
                sender switch
                {
                    EmailSender.Product => Product,
                    _ => Transactional
                };

            return new EmailSenderSettings
            {
                FromEmail = settings.FromEmail,
                FromName = settings.FromName,

                Username =
                    string.IsNullOrWhiteSpace(settings.Username)
                        ? Username
                        : settings.Username,

                Password =
                    string.IsNullOrWhiteSpace(settings.Password)
                        ? Password
                        : settings.Password
            };
        }
    }

    /// <summary>One From address and the credential that is allowed to use it.</summary>
    public sealed class EmailSenderSettings
    {
        public string FromEmail { get; set; } = string.Empty;

        public string FromName { get; set; } = string.Empty;

        /// <summary>Optional — falls back to <see cref="EmailSettings.Username"/>.</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Optional — falls back to <see cref="EmailSettings.Password"/>.</summary>
        public string Password { get; set; } = string.Empty;
    }
}
