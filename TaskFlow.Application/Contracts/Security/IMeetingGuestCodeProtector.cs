namespace TaskFlow.Application.Contracts.Security;

public interface IMeetingGuestCodeProtector
{
    string GenerateCode();
    string Protect(int accessLinkId, string normalizedEmail, string code);
    bool Verify(int accessLinkId, string normalizedEmail, string code, string protectedCode);
}
