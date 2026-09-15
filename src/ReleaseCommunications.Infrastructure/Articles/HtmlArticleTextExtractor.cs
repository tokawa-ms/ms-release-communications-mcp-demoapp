using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace ReleaseCommunications.Infrastructure.Articles;

public static partial class HtmlArticleTextExtractor
{
    public const int MaximumTextLength = 40_000;

    private static readonly string[] ContentSelectors =
        ["main article", "article", "main", "[role='main']", ".content", "#content"];
    private static readonly string[] BlockSelectors =
        ["h1", "h2", "h3", "h4", "h5", "h6", "p", "li", "blockquote", "pre", "td"];

    public static async Task<string> ExtractAsync(
        string html,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new FormatException("The linked Microsoft page returned empty HTML.");
        }

        var document = await new HtmlParser().ParseDocumentAsync(html, cancellationToken);
        foreach (var element in document.QuerySelectorAll(
                     "script, style, noscript, template, svg, nav, footer, header, aside, form"))
        {
            element.Remove();
        }

        var content = ContentSelectors
            .Select(document.QuerySelector)
            .FirstOrDefault(element => element is not null) ??
            document.Body ??
            throw new FormatException("The linked Microsoft page did not contain a readable body.");

        var blocks = content.QuerySelectorAll(string.Join(", ", BlockSelectors));
        var normalized = blocks
            .Select(block => NormalizeWhitespace(block.TextContent))
            .Where(text => text.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var text = normalized.Length > 0
            ? string.Join(Environment.NewLine + Environment.NewLine, normalized)
            : NormalizeWhitespace(content.TextContent);

        if (text.Length < 80)
        {
            throw new FormatException(
                $"The linked Microsoft page did not contain enough meaningful article text ({text.Length} characters).");
        }

        return BoundAtParagraph(text, MaximumTextLength);
    }

    private static string NormalizeWhitespace(string value) =>
        WhitespaceRegex().Replace(value, " ").Trim();

    private static string BoundAtParagraph(string value, int maximumLength)
    {
        if (value.Length <= maximumLength)
        {
            return value;
        }

        const string marker = "\n\n[Content truncated.]";
        var contentLimit = maximumLength - marker.Length;
        var boundary = value.LastIndexOf(
            Environment.NewLine + Environment.NewLine,
            contentLimit,
            StringComparison.Ordinal);
        return $"{value[..(boundary >= contentLimit / 2 ? boundary : contentLimit)].TrimEnd()}{marker}";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
