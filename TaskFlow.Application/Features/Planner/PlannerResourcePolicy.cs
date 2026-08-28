using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TaskFlow.Application.Exceptions;

namespace TaskFlow.Application.Features.Planner;

internal static class PlannerResourcePolicy
{
    public const long MaxFileSize = 25 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string[]> Allowed =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = [".pdf"],
            ["image/jpeg"] = [".jpg", ".jpeg"], ["image/png"] = [".png"],
            ["image/gif"] = [".gif"], ["image/webp"] = [".webp"],
            ["audio/mpeg"] = [".mp3"], ["audio/wav"] = [".wav"], ["audio/ogg"] = [".ogg"],
            ["audio/mp4"] = [".m4a"], ["video/mp4"] = [".mp4"],
            ["video/webm"] = [".webm"], ["video/quicktime"] = [".mov"],
            ["text/plain"] = [".txt"], ["text/markdown"] = [".md"], ["text/csv"] = [".csv"],
            ["application/json"] = [".json"], ["application/zip"] = [".zip"],
            ["application/msword"] = [".doc"],
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = [".docx"],
            ["application/vnd.ms-excel"] = [".xls"],
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = [".xlsx"],
            ["application/vnd.ms-powerpoint"] = [".ppt"],
            ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = [".pptx"]
        };

    public static string ValidateAndSanitize(string fileName, string contentType, long size)
    {
        if (size <= 0) throw new BusinessException("PLANNER_FILE_EMPTY", "Choose a non-empty file.");
        if (size > MaxFileSize) throw new BusinessException("PLANNER_FILE_TOO_LARGE", "Planner files cannot exceed 25 MB.");
        var safe = Path.GetFileName(fileName ?? string.Empty).Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars()) safe = safe.Replace(invalid, '_');
        if (safe.Length is < 1 or > 255) throw new BusinessException("PLANNER_FILE_NAME_INVALID", "The file name is invalid or too long.");
        if (!Allowed.TryGetValue(contentType ?? string.Empty, out var extensions) ||
            !extensions.Contains(Path.GetExtension(safe), StringComparer.OrdinalIgnoreCase))
            throw new BusinessException("PLANNER_FILE_TYPE_UNSUPPORTED", "This file type is not supported, or its extension does not match its content type.");
        return safe;
    }

    public static string Sha256(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    public static void EnsureContentMatchesType(string contentType, string fileName, ReadOnlySpan<byte> content)
    {
        var extension = Path.GetExtension(fileName);
        var valid = contentType switch
        {
            "application/pdf" => StartsWith(content, "%PDF-"u8),
            "image/jpeg" => content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF,
            "image/png" => content.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/gif" => StartsWith(content, "GIF87a"u8) || StartsWith(content, "GIF89a"u8),
            "image/webp" => content.Length >= 12 && StartsWith(content, "RIFF"u8) &&
                            content[8..].StartsWith("WEBP"u8),
            "audio/mpeg" => StartsWith(content, "ID3"u8) ||
                            (content.Length >= 2 && content[0] == 0xFF && (content[1] & 0xE0) == 0xE0),
            "audio/wav" => content.Length >= 12 && StartsWith(content, "RIFF"u8) &&
                           content[8..].StartsWith("WAVE"u8),
            "audio/ogg" => StartsWith(content, "OggS"u8),
            "audio/mp4" or "video/mp4" or "video/quicktime" =>
                content.Length >= 12 && content[4..].StartsWith("ftyp"u8),
            "video/webm" => content.StartsWith(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }),
            "application/zip" => IsZip(content),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => IsZip(content),
            "application/msword" or "application/vnd.ms-excel" or "application/vnd.ms-powerpoint" =>
                content.StartsWith(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }),
            "application/json" => IsJson(content),
            "text/plain" or "text/markdown" or "text/csv" => IsText(content),
            _ => false
        };

        if (!valid)
        {
            throw new BusinessException(
                "PLANNER_FILE_SIGNATURE_MISMATCH",
                $"The file content does not match the {extension} file type.");
        }
    }

    private static bool StartsWith(ReadOnlySpan<byte> content, ReadOnlySpan<byte> signature) =>
        content.StartsWith(signature);

    private static bool IsZip(ReadOnlySpan<byte> content) =>
        content.StartsWith(new byte[] { 0x50, 0x4B, 0x03, 0x04 }) ||
        content.StartsWith(new byte[] { 0x50, 0x4B, 0x05, 0x06 }) ||
        content.StartsWith(new byte[] { 0x50, 0x4B, 0x07, 0x08 });

    private static bool IsText(ReadOnlySpan<byte> content)
    {
        if (content.Contains((byte)0)) return false;
        try
        {
            _ = new UTF8Encoding(false, true).GetString(content);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static bool IsJson(ReadOnlySpan<byte> content)
    {
        try
        {
            using var _ = JsonDocument.Parse(content.ToArray(), new JsonDocumentOptions { MaxDepth = 64 });
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool CanPreviewInline(string contentType) => contentType == "application/pdf" ||
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
        contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
        contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
        contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
}
