using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReleaseCommunications.Core.Models;

namespace ReleaseCommunications.Core.UseCases;

public static class SummaryPromptBuilder
{
    public static string Build(EnrichedReleaseArticle enriched, SummaryLanguage language)
    {
        var article = enriched.AuthoritativeArticle;
        var languageInstruction = language == SummaryLanguage.Japanese
            ? "Respond only in Japanese."
            : "Respond only in English.";
        var diagnostics = enriched.Diagnostics.Count == 0
            ? "None"
            : string.Join(Environment.NewLine, enriched.Diagnostics.Select(value => $"- {value}"));
        var linkedContent = string.IsNullOrWhiteSpace(enriched.LinkedPageText)
            ? "Unavailable. Do not imply that the linked Microsoft page was read."
            : enriched.LinkedPageText;

        return $$"""
            {{languageInstruction}}
            Summarize only the supplied authoritative MRC metadata and linked Microsoft article content.
            Do not add facts that are not present. Treat the content-completeness status and diagnostics
            as authoritative: never imply that linked page content was read when it is unavailable.
            Return strict JSON with this exact shape:
            {"overview":"short overview","keyPoints":["point"],"impact":"customer impact","importantDates":"important dates or Unknown"}

            Content completeness: {{enriched.ContentStatus}}
            Enrichment diagnostics:
            {{diagnostics}}

            Authoritative MRC metadata:
            Title: {{article.Title}}
            Source: {{article.Source}}
            Products: {{string.Join(", ", article.Products)}}
            Status: {{article.Status ?? "Unknown"}}
            Last updated: {{Format(article.Dates.LastUpdatedAt)}}
            Published: {{Format(article.Dates.PublishedAt)}}
            General availability: {{article.Dates.GeneralAvailability ?? "Unknown"}}
            Preview availability: {{article.Dates.PreviewAvailability ?? "Unknown"}}
            Source URLs: {{FormatUris(enriched.SourceUris)}}
            Complete MRC description:
            {{article.Description}}

            Linked Microsoft article URL: {{enriched.FetchedPageUri?.ToString() ?? "Unavailable"}}
            Linked Microsoft article content:
            {{linkedContent}}
            """;
    }

    private static string Format(DateTimeOffset? value) =>
        value?.ToString("O") ?? "Unknown";

    private static string FormatUris(IReadOnlyList<Uri> values) =>
        values.Count == 0 ? "None" : string.Join(", ", values);
}

public static class SummaryResponseParser
{
    public static ArticleSummary Parse(string json, SummaryLanguage language, DateTimeOffset now)
    {
        using var document = JsonDocument.Parse(RemoveCodeFence(json));
        var root = document.RootElement;
        var overview = RequiredString(root, "overview");
        var impact = RequiredString(root, "impact");
        var importantDates = RequiredString(root, "importantDates");

        if (!root.TryGetProperty("keyPoints", out var pointsElement) ||
            pointsElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Summary keyPoints must be an array.");
        }

        var points = pointsElement.EnumerateArray()
            .Select(item => item.GetString()?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray();

        if (points.Length == 0)
        {
            throw new FormatException("Summary keyPoints must contain at least one item.");
        }

        return new ArticleSummary(overview, points, impact, importantDates, language, now);
    }

    private static string RequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            throw new FormatException($"Summary property '{propertyName}' is missing.");
        }

        var value = element.GetString()?.Trim();
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new FormatException($"Summary property '{propertyName}' is empty.");
    }

    private static string RemoveCodeFence(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstLineEnd = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return firstLineEnd >= 0 && lastFence > firstLineEnd
            ? trimmed[(firstLineEnd + 1)..lastFence].Trim()
            : trimmed;
    }
}

public static class SummaryCacheKey
{
    public static string Create(
        EnrichedReleaseArticle enriched,
        SummaryLanguage language,
        string deploymentName)
    {
        var article = enriched.AuthoritativeArticle;
        var contentHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    $"{article.Description}\n{enriched.LinkedPageText ?? string.Empty}")));
        var raw = string.Join(
            "|",
            "enriched-v1",
            article.StableId,
            article.Dates.LastUpdatedAt?.ToUniversalTime().ToString("O") ?? "unknown",
            enriched.ContentStatus,
            contentHash,
            language,
            deploymentName.Trim());

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }
}
