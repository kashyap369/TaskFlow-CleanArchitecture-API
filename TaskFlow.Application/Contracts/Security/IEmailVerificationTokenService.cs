namespace TaskFlow.Application.Contracts.Security
{
    /// <summary>
    /// Issues and validates the token that proves someone controls the email
    /// address they registered with.
    ///
    /// The token is <b>stateless and signed</b> (HMAC over the user id and an
    /// expiry) rather than a row in the database: it needs no schema change,
    /// cannot be forged without the server secret, and expires on its own. It
    /// needs no individual revocation — verifying twice is a no-op, because
    /// <c>User.VerifyEmail()</c> returns early once the user is verified.
    /// </summary>
    public interface IEmailVerificationTokenService
    {
        /// <summary>Issues a token for this user, valid for a fixed window.</summary>
        string Generate(int userId);

        /// <summary>
        /// Verifies signature and expiry, yielding the user id. Returns false
        /// for a malformed, tampered or expired token.
        /// </summary>
        bool TryValidate(
            string token,
            out int userId);
    }
}
