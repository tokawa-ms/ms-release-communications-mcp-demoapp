using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ReleaseCommunications.Core.Abstractions;
using ReleaseCommunications.Core.Models;

namespace ReleaseCommunications.Infrastructure.Mrc;

public sealed class MrcReleaseClient(
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : IMrcReleaseClient
{
    public static readonly Uri Endpoint = new("https://www.microsoft.com/releasecommunications/mcp");

    private static readonly string[] Microsoft365ListAliases =
        ["get_recent_m365_roadmaps", "get_recent_roadmaps"];
    private static readonly string[] Microsoft365DetailAliases =
        ["get_m365_roadmap_by_id", "get_roadmap_by_id"];
    private static readonly string[] AzureDetailAliases =
        ["get_azure_update_by_id"];

    public async Task<SourceFetchResult> FetchAsync(
        ReleaseSource source,
        ReleaseQuery query,
        CancellationToken cancellationToken)
    {
        if (source is not (ReleaseSource.Microsoft365 or ReleaseSource.Azure))
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        await using var client = await CreateClientAsync(cancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
        var listTool = ResolveToolName(
            tools.Select(tool => tool.Name),
            source == ReleaseSource.Azure ? ["get_recent_azure_updates"] : Microsoft365ListAliases);

        var articles = new List<ReleaseArticle>();
        if (query.PublishedWithinDays is > 0)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-query.PublishedWithinDays.Value);
            var windowEnd = DateTimeOffset.UtcNow.AddMinutes(1);
            while (windowEnd > cutoff && articles.Count < query.MaximumItems)
            {
                var windowStart = windowEnd.AddDays(-7);
                if (windowStart < cutoff)
                {
                    windowStart = cutoff;
                }

                await FetchPagesAsync(
                    $"created ge {windowStart:yyyy-MM-ddTHH:mm:ssZ} and created lt {windowEnd:yyyy-MM-ddTHH:mm:ssZ}");
                windowEnd = windowStart;
            }
        }
        else
        {
            await FetchPagesAsync(null, stopAfterMaximum: true);
        }

        var latestPublished = articles
            .GroupBy(article => article.StableId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderByDescending(article => article.Dates.PublishedAt)
            .ThenBy(article => article.StableId, StringComparer.Ordinal)
            .Take(query.MaximumItems)
            .ToArray();
        return new SourceFetchResult(source, latestPublished);

        async Task FetchPagesAsync(string? filter, bool stopAfterMaximum = false)
        {
            for (var skip = 0; ;)
            {
                var arguments = new Dictionary<string, object?>
                {
                    ["skip"] = skip,
                    ["include_facets"] = false
                };
                if (filter is not null)
                {
                    arguments["filter"] = filter;
                }

                var result = await client.CallToolAsync(
                    listTool,
                    arguments,
                    cancellationToken: cancellationToken);
                var page = MrcPayloadParser.ParsePage(GetJson(result), source);
                articles.AddRange(page.Articles);

                if (!page.HasMore ||
                    page.Articles.Count == 0 ||
                    (stopAfterMaximum && articles.Count >= query.MaximumItems))
                {
                    break;
                }

                skip = page.Offset + page.Limit;
            }
        }
    }

    public async Task<ConnectionDiagnostic> CheckConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var client = await CreateClientAsync(cancellationToken);
            var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
            var names = tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);
            var hasM365 = Microsoft365ListAliases.Any(names.Contains);
            var hasAzure = names.Contains("get_recent_azure_updates");
            return hasM365 && hasAzure
                ? new ConnectionDiagnostic("MRC", true, $"Connected. {tools.Count} tools discovered.")
                : new ConnectionDiagnostic("MRC", false, "Connected, but expected roadmap tools were not found.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ConnectionDiagnostic("MRC", false, ex.Message);
        }
    }

    public async Task<ReleaseArticle> FetchDetailAsync(
        ReleaseArticle article,
        CancellationToken cancellationToken)
    {
        await using var client = await CreateClientAsync(cancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
        var detailTool = ResolveToolName(
            tools.Select(tool => tool.Name),
            article.Source == ReleaseSource.Azure
                ? AzureDetailAliases
                : Microsoft365DetailAliases);
        var result = await client.CallToolAsync(
            detailTool,
            new Dictionary<string, object?> { ["id"] = article.SourceId },
            cancellationToken: cancellationToken);
        var detail = MrcPayloadParser.ParseDetail(GetJson(result), article.Source);
        if (!detail.SourceId.Equals(article.SourceId, StringComparison.Ordinal))
        {
            throw new FormatException(
                $"MRC detail response ID '{detail.SourceId}' did not match requested ID '{article.SourceId}'.");
        }

        return detail;
    }

    private async Task<McpClient> CreateClientAsync(CancellationToken cancellationToken)
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = Endpoint,
                Name = "Microsoft Release Communications",
                TransportMode = HttpTransportMode.StreamableHttp,
                ConnectionTimeout = TimeSpan.FromSeconds(30)
            },
            httpClientFactory.CreateClient("Mrc"),
            loggerFactory,
            ownsHttpClient: true);

        return await McpClient.CreateAsync(
            transport,
            new McpClientOptions
            {
                ProtocolVersion = "2025-06-18",
                InitializationTimeout = TimeSpan.FromSeconds(30)
            },
            loggerFactory: loggerFactory,
            cancellationToken: cancellationToken);
    }

    private static string ResolveToolName(IEnumerable<string> available, IEnumerable<string> aliases)
    {
        var names = available.ToHashSet(StringComparer.Ordinal);
        return aliases.FirstOrDefault(names.Contains) ??
            throw new InvalidOperationException(
                $"MRC server is missing an expected tool. Available: {string.Join(", ", names)}");
    }

    private static string GetJson(CallToolResult result)
    {
        if (result.IsError == true)
        {
            var error = string.Join(
                Environment.NewLine,
                result.Content.OfType<TextContentBlock>().Select(block => block.Text));
            throw new InvalidOperationException($"MRC tool returned an error: {error}");
        }

        if (result.StructuredContent is not null)
        {
            return result.StructuredContent.Value.GetRawText();
        }

        var text = result.Content.OfType<TextContentBlock>().Select(block => block.Text).FirstOrDefault();
        return !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new FormatException("MRC tool response did not contain JSON text.");
    }
}
