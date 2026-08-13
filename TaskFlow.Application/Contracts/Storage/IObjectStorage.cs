namespace TaskFlow.Application.Contracts.Storage;

public interface IObjectStorage
{
    Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default);

    Task UploadAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<StoredObject> DownloadAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken = default);
}

public sealed record StoredObject(byte[] Content, string ContentType);
