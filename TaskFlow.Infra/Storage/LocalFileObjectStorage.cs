using Microsoft.Extensions.Options;
using TaskFlow.Application.Contracts.Storage;

namespace TaskFlow.Infra.Storage;

/// <summary>
/// Development-only object storage that keeps uploaded files on the local disk.
/// Production continues to use the S3-compatible provider.
/// </summary>
public sealed class LocalFileObjectStorage : IObjectStorage
{
    private readonly string _rootPath;

    public LocalFileObjectStorage(IOptions<ObjectStorageSettings> settings)
    {
        var configuredPath = settings.Value.LocalPath;
        _rootPath = Path.GetFullPath(configuredPath, AppContext.BaseDirectory);
    }

    public Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_rootPath);
        return Task.CompletedTask;
    }

    public async Task UploadAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using (var file = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        await File.WriteAllTextAsync(
            GetContentTypePath(path),
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            cancellationToken);
    }

    public async Task<StoredObject> DownloadAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(objectKey);
        var content = await File.ReadAllBytesAsync(path, cancellationToken);
        var contentTypePath = GetContentTypePath(path);
        var contentType = File.Exists(contentTypePath)
            ? await File.ReadAllTextAsync(contentTypePath, cancellationToken)
            : "application/octet-stream";

        return new StoredObject(content, contentType);
    }

    public Task DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(objectKey);
        File.Delete(path);
        File.Delete(GetContentTypePath(path));
        return Task.CompletedTask;
    }

    private string ResolvePath(string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        var relativePath = objectKey
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(relativePath, _rootPath);
        var rootWithSeparator = _rootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Object key must remain within local object storage.", nameof(objectKey));
        }

        return fullPath;
    }

    private static string GetContentTypePath(string objectPath) => $"{objectPath}.content-type";
}
