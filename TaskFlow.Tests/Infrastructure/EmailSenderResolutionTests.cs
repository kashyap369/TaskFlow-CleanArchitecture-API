using Microsoft.Extensions.Configuration;
using TaskFlow.Application.Contracts.Email;
using TaskFlow.Infra.Email;

namespace TaskFlow.Tests.Infrastructure;

/// <summary>
/// Guards the From address each sender resolves to. Getting this wrong does not
/// throw and does not fail the boot — mailcow refuses the message with
/// "not permitted to send as", hours later, to a user who never sees it.
/// </summary>
public sealed class EmailSenderResolutionTests
{
    [Fact]
    public void EachSender_ResolvesItsOwnFromAddress()
    {
        var settings = Bind(Production());

        Assert.Equal("noreply@inksphere.space", settings.For(EmailSender.Transactional).FromEmail);
        Assert.Equal("taskflow@inksphere.space", settings.For(EmailSender.Product).FromEmail);
    }

    [Fact]
    public void EachSender_UsesItsOwnCredential_WhenItHasOne()
    {
        var settings = Bind(Production());

        Assert.Equal("noreply@inksphere.space", settings.For(EmailSender.Transactional).Username);
        Assert.Equal("transactional-secret", settings.For(EmailSender.Transactional).Password);

        Assert.Equal("taskflow@inksphere.space", settings.For(EmailSender.Product).Username);
        Assert.Equal("product-secret", settings.For(EmailSender.Product).Password);
    }

    [Fact]
    public void SenderWithoutCredential_FallsBackToTheSharedPair()
    {
        // The single-app-password deployment: Product defines a From address but no
        // credential, so it must authenticate as the shared mailbox. This only
        // delivers if mailcow's "Allow to send as" permits it.
        var config = Production();
        config.Remove("EmailSettings:Product:Username");
        config.Remove("EmailSettings:Product:Password");

        var product = Bind(config).For(EmailSender.Product);

        Assert.Equal("noreply@inksphere.space", product.Username);
        Assert.Equal("shared-secret", product.Password);
        Assert.Equal("taskflow@inksphere.space", product.FromEmail);
    }

    [Fact]
    public void FlatDoubleUnderscoreKeys_BindTheSameAsNestedOnes()
    {
        // How the vault actually supplies these: EmailSettings__Product__FromEmail.
        // A single underscore or a typo binds to nothing and mail silently stops.
        var settings =
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["EmailSettings:Host"] = "mail.buildbykashyap.in",
                    ["EmailSettings:Product:FromEmail"] = "taskflow@inksphere.space"
                })
                .Build()
                .GetSection("EmailSettings")
                .Get<EmailSettings>()!;

        Assert.Equal("mail.buildbykashyap.in", settings.Host);
        Assert.Equal("taskflow@inksphere.space", settings.For(EmailSender.Product).FromEmail);
    }

    [Fact]
    public void UnconfiguredSender_ResolvesToAnEmptyFrom_SoTheSenderCanRefuse()
    {
        var settings = Bind(new Dictionary<string, string?>
        {
            ["EmailSettings:Host"] = "mail.buildbykashyap.in"
        });

        Assert.Equal(string.Empty, settings.For(EmailSender.Product).FromEmail);
    }

    private static Dictionary<string, string?> Production() =>
        new()
        {
            ["EmailSettings:Host"] = "mail.buildbykashyap.in",
            ["EmailSettings:Port"] = "587",
            ["EmailSettings:EnableSsl"] = "true",
            ["EmailSettings:Username"] = "noreply@inksphere.space",
            ["EmailSettings:Password"] = "shared-secret",
            ["EmailSettings:Transactional:FromEmail"] = "noreply@inksphere.space",
            ["EmailSettings:Transactional:FromName"] = "TaskFlow",
            ["EmailSettings:Transactional:Password"] = "transactional-secret",
            ["EmailSettings:Product:FromEmail"] = "taskflow@inksphere.space",
            ["EmailSettings:Product:FromName"] = "TaskFlow",
            ["EmailSettings:Product:Username"] = "taskflow@inksphere.space",
            ["EmailSettings:Product:Password"] = "product-secret"
        };

    private static EmailSettings Bind(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build()
            .GetSection("EmailSettings")
            .Get<EmailSettings>()!;
}
