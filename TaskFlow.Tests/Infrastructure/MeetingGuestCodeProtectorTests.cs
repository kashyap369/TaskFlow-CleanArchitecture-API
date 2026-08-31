using Microsoft.Extensions.Options;
using TaskFlow.Infra.Security;

namespace TaskFlow.Tests.Infrastructure;

public sealed class MeetingGuestCodeProtectorTests
{
    [Fact]
    public void Codes_AreSixDigits_LinkAndEmailScoped_AndComparedSafely()
    {
        var protector = new MeetingGuestCodeProtector(Options.Create(new OneTimeCodeSettings
        { SecretKey = "meeting-phase-three-test-secret-at-least-32-characters" }));
        var code = protector.GenerateCode(); var hash = protector.Protect(17, "GUEST@EXAMPLE.TEST", code);
        Assert.Matches("^[0-9]{6}$", code); Assert.DoesNotContain(code, hash);
        Assert.True(protector.Verify(17, "GUEST@EXAMPLE.TEST", code, hash));
        Assert.False(protector.Verify(18, "GUEST@EXAMPLE.TEST", code, hash));
        Assert.False(protector.Verify(17, "OTHER@EXAMPLE.TEST", code, hash));
        Assert.False(protector.Verify(17, "GUEST@EXAMPLE.TEST", "000000", hash));
    }
}
