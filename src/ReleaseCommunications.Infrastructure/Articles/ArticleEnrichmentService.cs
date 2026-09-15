using ReleaseCommunications.Core.Abstractions;
using ReleaseCommunications.Core.Models;

namespace ReleaseCommunications.Infrastructure.Articles;

public sealed class ArticleEnrichmentService(
    IMrcReleaseClient mrcClient,
    IArticleContentClient articleContentClient) : IArticleEnrichmentService
{
    public async Task<EnrichedReleaseArticle> EnrichAsync(
        ReleaseArticle article,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<string>();
        ReleaseArticle authoritativeArticle;
        var detailSucceeded = false;
        try
        {
            authoritativeArticle = await mrcClient.FetchDetailAsync(article, cancellationToken);
            detailSucceeded = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            authoritativeArticle = article;
            diagnostics.Add(
                $"MRC detail retrieval failed; the list description is the only MRC text available: {ex.Message}");
        }

        var sourceUris = authoritativeArticle.SourceUris
            .Concat(article.SourceUris)
            .DistinctBy(uri => uri.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var allowedUris = sourceUris.Where(MicrosoftArticleUrlPolicy.IsAllowed).ToArray();
        var trustedUris = allowedUris.Take(3).ToArray();
        foreach (var rejected in sourceUris.Where(uri => !MicrosoftArticleUrlPolicy.IsAllowed(uri)))
        {
            diagnostics.Add($"Skipped untrusted or non-HTTPS source URL: {rejected}");
        }
        if (allowedUris.Length > trustedUris.Length)
        {
            diagnostics.Add(
                $"Skipped {allowedUris.Length - trustedUris.Length} additional trusted source URL(s) after the fetch-attempt limit.");
        }

        foreach (var sourceUri in trustedUris)
        {
            var page = await articleContentClient.FetchAsync(sourceUri, cancellationToken);
            if (page.IsSuccessful)
            {
                return new EnrichedReleaseArticle(
                    article,
                    authoritativeArticle,
                    sourceUris,
                    page.FinalUri,
                    page.Text,
                    ArticleContentStatus.FullLinkedArticle,
                    diagnostics);
            }

            diagnostics.Add(page.Diagnostic);
        }

        if (sourceUris.Length == 0)
        {
            diagnostics.Add("MRC detail did not provide a linked source URL.");
        }
        else if (trustedUris.Length == 0)
        {
            diagnostics.Add("MRC detail did not provide a trusted Microsoft HTTPS article URL.");
        }

        return new EnrichedReleaseArticle(
            article,
            authoritativeArticle,
            sourceUris,
            null,
            null,
            detailSucceeded
                ? ArticleContentStatus.MrcDetailOnly
                : ArticleContentStatus.ListDescriptionOnly,
            diagnostics);
    }
}
