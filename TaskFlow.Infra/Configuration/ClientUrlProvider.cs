using Microsoft.Extensions.Options;
using TaskFlow.Application.Contracts.Configuration;

namespace TaskFlow.Infra.Configuration;

public sealed class ClientUrlProvider : IClientUrlProvider
{
    public ClientUrlProvider(IOptions<ClientSettings> settings)
    {
        var configuredBaseUrl = settings.Value.BaseUrl.TrimEnd('/');

        BaseUrl = configuredBaseUrl.Replace(
            "https://tasflow.inksphere.space",
            "https://taskflow.inksphere.space",
            StringComparison.OrdinalIgnoreCase);
    }

    public string BaseUrl { get; }
}
