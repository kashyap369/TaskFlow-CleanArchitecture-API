namespace TaskFlow.Application.Contracts.Email
{
    /// <summary>
    /// Which of TaskFlow's sending identities a message goes out as.
    /// <para>
    /// This is a deliberate split, not a cosmetic one. Mail that a user must
    /// act on to get into their account travels under a different reputation
    /// and a different mailbox from mail TaskFlow sends to be friendly, so
    /// that a spam complaint about the latter cannot stop the former from
    /// being delivered.
    /// </para>
    /// </summary>
    public enum EmailSender
    {
        /// <summary>
        /// <c>noreply@</c> — machine mail the recipient must act on: email
        /// verification, sign-in and password-reset codes, invitations,
        /// meeting guest codes. Nobody reads replies to this address, and
        /// the body should never invite one.
        /// <para>
        /// This is the default for anything new. A message that reaches a
        /// user by mistake under <c>noreply@</c> is an annoyance; the same
        /// message under a branded address is a reputation problem.
        /// </para>
        /// </summary>
        Transactional = 0,

        /// <summary>
        /// <c>taskflow@</c> — mail TaskFlow sends as a product rather than as
        /// a machine: the thank-you after a new account is verified, and
        /// anything later that is about the relationship rather than about a
        /// pending action. Replies to this address reach a real mailbox.
        /// </summary>
        Product = 1
    }
}
