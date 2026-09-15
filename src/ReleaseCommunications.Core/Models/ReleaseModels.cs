namespace ReleaseCommunications.Core.Models;

public enum ReleaseSource
{
    All,
    Microsoft365,
    Azure
}

public enum SummaryLanguage
{
    English,
    Japanese
}

public enum UiLanguage
{
    Japanese,
    English
}

public enum SummaryState
{
    NotStarted,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

public enum ArticleContentStatus
{
    FullLinkedArticle,
    MrcDetailOnly,
    ListDescriptionOnly
}

public enum LoadState
{
    Idle,
    Loading,
    Success,
    Empty,
    PartialFailure,
    Error,
    Cancelled
}

public sealed record ReleaseDateInfo(
    DateTimeOffset? LastUpdatedAt,
    DateTimeOffset? PublishedAt,
    string? GeneralAvailability,
    string? PreviewAvailability);

public sealed record ArticleSummary(
    string Overview,
    IReadOnlyList<string> KeyPoints,
    string Impact,
    string ImportantDates,
    SummaryLanguage Language,
    DateTimeOffset GeneratedAt)
{
    public ArticleContentStatus ContentStatus { get; init; } = ArticleContentStatus.ListDescriptionOnly;
    public IReadOnlyList<string> ContentDiagnostics { get; init; } = [];
}

public sealed record ReleaseArticle(
    string StableId,
    string SourceId,
    ReleaseSource Source,
    string Title,
    string Description,
    IReadOnlyList<string> Products,
    string? Status,
    IReadOnlyList<string> Tags,
    ReleaseDateInfo Dates,
    Uri? SourceUri,
    bool IsDescriptionTruncated)
{
    public IReadOnlyList<Uri> SourceUris { get; init; } =
        SourceUri is null ? [] : [SourceUri];
    public ArticleSummary? Summary { get; init; }
    public SummaryState SummaryState { get; init; }
    public string? SummaryError { get; init; }
}

public sealed record ArticlePageContent(
    bool IsSuccessful,
    Uri RequestedUri,
    Uri? FinalUri,
    string? Text,
    string Diagnostic);

public sealed record EnrichedReleaseArticle(
    ReleaseArticle ListedArticle,
    ReleaseArticle AuthoritativeArticle,
    IReadOnlyList<Uri> SourceUris,
    Uri? FetchedPageUri,
    string? LinkedPageText,
    ArticleContentStatus ContentStatus,
    IReadOnlyList<string> Diagnostics);

public sealed record ReleaseQuery(
    ReleaseSource Source,
    int MaximumItems,
    int? PublishedWithinDays = null,
    string? Product = null,
    string? Status = null,
    string? Tag = null);

public sealed record SourceFetchResult(
    ReleaseSource Source,
    IReadOnlyList<ReleaseArticle> Articles,
    string? Error = null);

public sealed record AggregatedReleaseResult(
    IReadOnlyList<ReleaseArticle> Articles,
    IReadOnlyList<SourceFetchResult> Sources)
{
    public bool IsPartialFailure =>
        Sources.Any(source => source.Error is not null) &&
        Sources.Any(source => source.Error is null);

    public bool IsTotalFailure => Sources.Count > 0 && Sources.All(source => source.Error is not null);
}

public sealed record AppSettings(
    string FoundryProjectEndpoint,
    string FoundryDeploymentName,
    SummaryLanguage DefaultSummaryLanguage,
    ReleaseSource DefaultSource,
    int MaximumItems,
    int SummaryConcurrency,
    int CacheRetentionDays,
    int MaximumCacheEntries,
    UiLanguage UiLanguage = UiLanguage.Japanese)
{
    public static AppSettings Default { get; } = new(
        string.Empty,
        string.Empty,
        SummaryLanguage.Japanese,
        ReleaseSource.All,
        50,
        3,
        14,
        500,
        UiLanguage.Japanese);
}

public sealed record ConnectionDiagnostic(string Component, bool IsSuccessful, string Message);
