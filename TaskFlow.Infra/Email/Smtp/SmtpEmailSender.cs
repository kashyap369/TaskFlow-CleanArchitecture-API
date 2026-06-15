using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

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
            bool isHtml = true)
        {
            using var client = new SmtpClient(
                _settings.Host,
                _settings.Port);

            client.Credentials =
                new NetworkCredential(
                    _settings.Username,
                    _settings.Password);

            client.EnableSsl =
                _settings.EnableSsl;

            var message = new MailMessage
            {
                From = new MailAddress(
                    _settings.FromEmail,
                    _settings.FromName),

                Subject = subject,

                Body = body,

                IsBodyHtml = isHtml
            };

            message.To.Add(to);

            await client.SendMailAsync(message);
        }
    }
}