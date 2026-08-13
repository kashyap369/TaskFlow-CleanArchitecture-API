namespace TaskFlow.Infra.Storage;

public sealed class ObjectStorageSettings
{
    public string Endpoint { get; set; } = string.Empty;

    public string PublicEndpoint { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string Bucket { get; set; } = string.Empty;

    public bool UseSsl { get; set; } = true;

    public bool ForcePathStyle { get; set; } = true;
}
