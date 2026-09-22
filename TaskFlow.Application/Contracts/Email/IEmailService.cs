namespace TaskFlow.Application.Contracts.Email
{
    public interface IEmailService
    {
        /// <summary>
        /// Sends one HTML message as the given identity.
        /// <para>
        /// <paramref name="sender"/> has no default on purpose: which address
        /// a message leaves under is a decision worth making at the point the
        /// message is composed, not one to inherit by accident.
        /// </para>
        /// </summary>
        Task SendAsync(
            string to,
            string subject,
            string body,
            EmailSender sender,
            CancellationToken cancellationToken = default);
    }
}
