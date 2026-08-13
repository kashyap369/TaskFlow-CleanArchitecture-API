using Microsoft.Extensions.Options;
using TaskFlow.Application.Contracts.Configuration;

namespace TaskFlow.Infra.Configuration;

public sealed class ClientUrlProvider : IClientUrlProvider
{
    public ClientUrlProvider(IOptions<ClientSettings> settings)
    {
        BaseUrl = settings.Value.BaseUrl.TrimEnd('/');
    }

    public string BaseUrl { get; }
}
