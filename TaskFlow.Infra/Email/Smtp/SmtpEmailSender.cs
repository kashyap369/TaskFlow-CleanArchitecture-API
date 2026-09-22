using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Contracts.Email;

namespace TaskFlow.Infra.Email.Smtp
{
    public sealed class SmtpEmailSender
    {
        private readonly EmailSettings _settings;

        public SmtpEmailSender(
            IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task SendAsync(
            string to,
            string subject,
            string body,
            EmailSender sender,
            bool isHtml = true,
            CancellationToken cancellationToken = default)
        {
            var identity = _settings.For(sender);

            if (string.IsNullOrWhiteSpace(identity.FromEmail))
                throw new InvalidOperationException(
                    $"EmailSettings:{sender}:FromEmail is not configured, so TaskFlow "
                    + "cannot send this message. Configure the sender or the mail is "
                    + "silently lost.");

            using var client = new SmtpClient(
                _settings.Host,
                _settings.Port);

            // STARTTLS on 587. See EmailSettings.Port — this is not implicit TLS,
            // and this client cannot do implicit TLS at all.
            client.EnableSsl =
                _settings.EnableSsl;

            client.Credentials =
                new NetworkCredential(
                    identity.Username,
                    identity.Password);

            using var message = new MailMessage
            {
                From = new MailAddress(
                    identity.FromEmail,
                    identity.FromName),

                Subject = subject,

                Body = body,

                IsBodyHtml = isHtml
            };

            message.To.Add(to);

            // SendMailAsync's overload without a token ignores cancellation
            // entirely; passing it lets a shutdown interrupt a hung connection
            // rather than holding the request open until the SMTP timeout.
            await client.SendMailAsync(
                message,
                cancellationToken);
        }
    }
}
