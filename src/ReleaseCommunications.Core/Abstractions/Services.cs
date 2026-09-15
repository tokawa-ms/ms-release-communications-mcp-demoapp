using ReleaseCommunications.Core.Models;

namespace ReleaseCommunications.Core.Abstractions;

public interface IMrcReleaseClient
{
    Task<SourceFetchResult> FetchAsync(
        ReleaseSource source,
        ReleaseQuery query,
        CancellationToken cancellationToken);

    Task<ReleaseArticle> FetchDetailAsync(
        ReleaseArticle article,
        CancellationToken cancellationToken);

    Task<ConnectionDiagnostic> CheckConnectionAsync(CancellationToken cancellationToken);
}

public interface IArticleContentClient
{
    Task<ArticlePageContent> FetchAsync(Uri uri, CancellationToken cancellationToken);
}

public interface IArticleEnrichmentService
{
    Task<EnrichedReleaseArticle> EnrichAsync(
        ReleaseArticle article,
        CancellationToken cancellationToken);
}

public interface IReleaseAggregationService
{
    Task<AggregatedReleaseResult> GetReleasesAsync(
        ReleaseQuery query,
        CancellationToken cancellationToken);

    IReadOnlyList<ReleaseArticle> FilterAndSort(
        IEnumerable<ReleaseArticle> articles,
        ReleaseQuery query,
        DateTimeOffset now);
}

public interface ISummarizationService
{
    Task<ArticleSummary> SummarizeAsync(
        EnrichedReleaseArticle article,
        SummaryLanguage language,
        CancellationToken cancellationToken);

    Task<ConnectionDiagnostic> CheckConnectionAsync(
        AppSettings settings,
        CancellationToken cancellationToken);
}

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}

public interface ISummaryCache
{
    Task<ArticleSummary?> TryGetAsync(string key, CancellationToken cancellationToken);
    Task SetAsync(string key, ArticleSummary summary, CancellationToken cancellationToken);
    Task PruneAsync(AppSettings settings, CancellationToken cancellationToken);
}
