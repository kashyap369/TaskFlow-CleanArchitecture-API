using System.Security.Cryptography;
using TaskFlow.Application.Contracts.Storage;
using TaskFlow.Application.Exceptions;

namespace TaskFlow.Application.Features.Attendance;

/// <summary>
/// The selfie a punch must carry. The type is decided from the file's own bytes, never from its
/// name or the Content-Type the phone sent — those are only claims.
/// </summary>
internal static class AttendancePhotoPolicy
{
    // The app compresses to a few hundred KB; this is the ceiling, not the expectation.
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly string[] HeifBrands = ["heic", "heix", "hevc", "hevx", "heim", "heis", "mif1", "msf1"];

    public sealed record Photo(byte[] Content, string ContentType, string Sha256);

    public static async Task<Photo> ReadAsync(Stream? content, long length, CancellationToken cancellationToken)
    {
        if (content is null || length <= 0)
            throw new BusinessException("ATTENDANCE_SELFIE_REQUIRED", "Take a selfie to punch in or out.");
        if (length > MaxBytes)
            throw new BusinessException("ATTENDANCE_SELFIE_TOO_LARGE", "The selfie cannot exceed 5 MB.");

        using var buffer = new MemoryStream((int)length);
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length is 0 or > MaxBytes)
            throw new BusinessException("ATTENDANCE_SELFIE_REQUIRED", "Take a selfie to punch in or out.");

        var bytes = buffer.ToArray();
        var contentType = DetectImageType(bytes) ??
            throw new BusinessException("ATTENDANCE_SELFIE_TYPE_UNSUPPORTED", "The selfie must be a JPEG, PNG or HEIC photo.");
        return new Photo(bytes, contentType, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    public static string StorageKey(int organizationId, int userId, string kind) =>
        $"attendance/{organizationId}/{userId}/{Guid.NewGuid():N}-{kind}";

    /// <summary>
    /// Best effort: used when the punch failed after the upload, so storage does not collect
    /// selfies no row points at. Never throws over the original failure.
    /// </summary>
    public static async Task TryDeleteAsync(IObjectStorage storage, string storageKey)
    {
        try { await storage.DeleteAsync(storageKey, CancellationToken.None); }
        catch (Exception) { /* the retention sweep never sees an orphan; worth no more than this */ }
    }

    private static string? DetectImageType(ReadOnlySpan<byte> content)
    {
        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
            return "image/jpeg";
        if (content.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return "image/png";
        // ISO base media: bytes 4-7 are "ftyp", 8-11 the brand.
        if (content.Length >= 12 && content.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            var brand = System.Text.Encoding.ASCII.GetString(content.Slice(8, 4));
            if (HeifBrands.Contains(brand)) return "image/heic";
        }
        return null;
    }
}
