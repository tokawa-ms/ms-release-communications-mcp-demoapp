using System.Globalization;
using System.Text.Json;
using ReleaseCommunications.Core.Models;

namespace ReleaseCommunications.Infrastructure.Mrc;

public static class MrcPayloadParser
{
    public static ParsedMrcPage ParsePage(string json, ReleaseSource source)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!TryGet(root, "items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("MRC response does not contain an items array.");
        }

        var articles = items.EnumerateArray().Select(item => ParseArticle(item, source)).ToArray();
        var hasMore = TryGet(root, "hasMore", out var hasMoreElement) &&
            hasMoreElement.ValueKind is JsonValueKind.True;
        var offset = GetInt32(root, "offset") ?? 0;
        var limit = GetInt32(root, "limit") ?? 50;
        var totalCount = GetInt32(root, "totalCount") ?? articles.Length;
        return new ParsedMrcPage(articles, offset, limit, totalCount, hasMore);
    }

    public static ReleaseArticle ParseDetail(string json, ReleaseSource source)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (TryGet(root, "item", out var item))
        {
            root = item;
        }

        return ParseArticle(root, source);
    }

    private static ReleaseArticle ParseArticle(JsonElement item, ReleaseSource source)
    {
        var id = RequiredValue(item, "id");
        var title = RequiredValue(item, "title");
        var description = RequiredValue(item, "description");
        var products = GetStrings(item, "products");
        var tags = source == ReleaseSource.Azure
            ? GetStrings(item, "tags").Concat(GetStrings(item, "productCategories")).Distinct().ToArray()
            : GetStrings(item, "releaseRings").Concat(GetStrings(item, "platforms")).Distinct().ToArray();

        var sourceUris = ParseUris(item);
        return new ReleaseArticle(
            $"{(source == ReleaseSource.Azure ? "azure" : "m365")}:{id}",
            id,
            source,
            title,
            description,
            products,
            GetString(item, "status"),
            tags,
            new ReleaseDateInfo(
                ParseDate(item, "modified"),
                ParseDate(item, "created"),
                GetString(item, "generalAvailabilityDate"),
                GetString(item, "previewAvailabilityDate")),
            sourceUris.Length > 0 ? sourceUris[0] : null,
            description.EndsWith("...", StringComparison.Ordinal) || description.Length >= 980)
        {
            SourceUris = sourceUris
        };
    }

    private static Uri[] ParseUris(JsonElement item)
    {
        var values = new List<Uri>();
        foreach (var propertyName in new[] { "url", "link", "moreInfoUrl" })
        {
            if (Uri.TryCreate(GetString(item, propertyName), UriKind.Absolute, out var uri))
            {
                values.Add(uri);
            }
        }

        if (TryGet(item, "moreInfoUrls", out var urls) && urls.ValueKind == JsonValueKind.Array)
        {
            foreach (var candidate in urls.EnumerateArray())
            {
                if (Uri.TryCreate(candidate.GetString(), UriKind.Absolute, out var uri))
                {
                    values.Add(uri);
                }
            }
        }

        return values.DistinctBy(uri => uri.AbsoluteUri, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static DateTimeOffset? ParseDate(JsonElement item, string propertyName)
    {
        var text = GetString(item, propertyName);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var value))
        {
            return value;
        }

        throw new FormatException($"MRC field '{propertyName}' contains an invalid date: {text}");
    }

    private static string RequiredValue(JsonElement item, string propertyName) =>
        GetString(item, propertyName) ??
        throw new FormatException($"MRC item is missing required field '{propertyName}'.");

    private static string? GetString(JsonElement item, string propertyName)
    {
        if (!TryGet(item, propertyName, out var element) ||
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : element.ToString();
    }

    private static int? GetInt32(JsonElement item, string propertyName) =>
        TryGet(item, propertyName, out var element) && element.TryGetInt32(out var value)
            ? value
            : null;

    private static string[] GetStrings(JsonElement item, string propertyName) =>
        TryGet(item, propertyName, out var element) && element.ValueKind == JsonValueKind.Array
            ? element.EnumerateArray()
                .Select(value => value.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToArray()
            : [];

    private static bool TryGet(JsonElement item, string propertyName, out JsonElement value)
    {
        foreach (var property in item.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}

public sealed record ParsedMrcPage(
    IReadOnlyList<ReleaseArticle> Articles,
    int Offset,
    int Limit,
    int TotalCount,
    bool HasMore);
