using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Contracts.Storage;

namespace TaskFlow.Infra.Storage;

public sealed class S3ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3 _client;
    private readonly ObjectStorageSettings _settings;

    public S3ObjectStorage(
        IAmazonS3 client,
        IOptions<ObjectStorageSettings> settings)
    {
        _client = client;
        _settings = settings.Value;
    }

    public async Task EnsureBucketExistsAsync(
        CancellationToken cancellationToken = default)
    {
        if (await AmazonS3Util.DoesS3BucketExistV2Async(_client, _settings.Bucket))
        {
            return;
        }

        await _client.PutBucketAsync(
            new PutBucketRequest { BucketName = _settings.Bucket },
            cancellationToken);
    }

    public async Task UploadAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        await _client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _settings.Bucket,
                Key = objectKey,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false
            },
            cancellationToken);
    }

    public async Task<StoredObject> DownloadAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        using var response = await _client.GetObjectAsync(
            _settings.Bucket,
            objectKey,
            cancellationToken);

        await using var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer, cancellationToken);

        return new StoredObject(
            buffer.ToArray(),
            response.Headers.ContentType ?? "application/octet-stream");
    }

    public Task DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        return _client.DeleteObjectAsync(_settings.Bucket, objectKey, cancellationToken);
    }
}
