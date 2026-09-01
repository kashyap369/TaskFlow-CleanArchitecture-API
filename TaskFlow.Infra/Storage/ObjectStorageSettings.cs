namespace TaskFlow.Infra.Storage;

public sealed class ObjectStorageSettings
{
    public string Provider { get; set; } = "S3";

    public string LocalPath { get; set; } = "App_Data/objects";

    public string Endpoint { get; set; } = string.Empty;

    public string PublicEndpoint { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string Bucket { get; set; } = string.Empty;

    public string Region { get; set; } = "us-east-1";

    public bool UseSsl { get; set; } = true;

    public bool ForcePathStyle { get; set; } = true;

    public bool UsesLocalFileSystem =>
        string.Equals(Provider, "Local", StringComparison.OrdinalIgnoreCase);
}
