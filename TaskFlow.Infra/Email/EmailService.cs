using TaskFlow.Application.Contracts.Email;
using TaskFlow.Infra.Email.Smtp;

namespace TaskFlow.Infra.Email
{
    public sealed class EmailService
        : IEmailService
    {
        private readonly SmtpEmailSender _smtpEmailSender;

        public EmailService(
            SmtpEmailSender smtpEmailSender)
        {
            _smtpEmailSender = smtpEmailSender;
        }

        public async Task SendAsync(
            string to,
            string subject,
            string body,
            CancellationToken cancellationToken = default)
        {
            await _smtpEmailSender.SendAsync(
                to,
                subject,
                body,
                true);
        }
    }
}