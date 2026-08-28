using System.Text.Json;
using System.Text;

namespace TaskFlow.Domain.Entities.Planner;

public static class PlannerSceneDocument
{
    public const int MaximumLength = 5_000_000;
    public const int MaximumElementCount = 5_000;
    public const int MaximumJsonDepth = 64;
    public const int MaximumStringLength = 250_000;
    public const int RevisionRetentionLimit = 100;

    public const string Empty =
        "{\"type\":\"excalidraw\",\"version\":2,\"source\":\"taskflow\",\"elements\":[],\"appState\":{},\"files\":{}}";

    public static void EnsureValid(string sceneJson)
    {
        if (string.IsNullOrWhiteSpace(sceneJson))
            throw new ArgumentException("Planner scene JSON is required.", nameof(sceneJson));

        if (Encoding.UTF8.GetByteCount(sceneJson) > MaximumLength)
            throw new ArgumentException("Planner scene JSON cannot exceed 5 MB.", nameof(sceneJson));

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(sceneJson, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = MaximumJsonDepth
            });
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Planner scene JSON is invalid.", nameof(sceneJson), exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Planner scene must be a JSON object.", nameof(sceneJson));

            if (!root.TryGetProperty("elements", out var elements) ||
                elements.ValueKind != JsonValueKind.Array)
            {
                throw new ArgumentException("Planner scene must contain an elements array.", nameof(sceneJson));
            }

            if (elements.GetArrayLength() > MaximumElementCount)
            {
                throw new ArgumentException(
                    $"Planner scenes cannot contain more than {MaximumElementCount:N0} elements.",
                    nameof(sceneJson));
            }

            if (root.TryGetProperty("type", out var sceneType) &&
                (sceneType.ValueKind != JsonValueKind.String || sceneType.GetString() != "excalidraw"))
            {
                throw new ArgumentException("Planner scene type must be excalidraw.", nameof(sceneJson));
            }

            if (root.TryGetProperty("files", out var files) &&
                files.ValueKind == JsonValueKind.Object &&
                files.EnumerateObject().Any())
            {
                throw new ArgumentException(
                    "Planner scenes cannot contain embedded files. Upload assets separately.",
                    nameof(sceneJson));
            }

            ValidateTree(root);
        }
    }

    private static void ValidateTree(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name.Length > 128)
                        throw new ArgumentException("Planner scene property names are too long.");

                    if (property.Name.Equals("link", StringComparison.OrdinalIgnoreCase) &&
                        property.Value.ValueKind == JsonValueKind.String)
                    {
                        EnsureSafeLink(property.Value.GetString());
                    }

                    ValidateTree(property.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) ValidateTree(item);
                break;
            case JsonValueKind.String:
                var value = element.GetString() ?? string.Empty;
                if (value.Length > MaximumStringLength || value.Contains('\0'))
                    throw new ArgumentException("Planner scene contains an invalid or oversized text value.");
                if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains(";base64,", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Planner scenes cannot contain embedded or base64 data.");
                break;
        }
    }

    private static void EnsureSafeLink(string? link)
    {
        if (string.IsNullOrWhiteSpace(link)) return;
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https" or "mailto"))
        {
            throw new ArgumentException("Planner element links must use http, https, or mailto.");
        }
    }
}
