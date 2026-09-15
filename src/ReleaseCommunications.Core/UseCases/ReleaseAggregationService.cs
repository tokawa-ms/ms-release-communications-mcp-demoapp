using ReleaseCommunications.Core.Abstractions;
using ReleaseCommunications.Core.Models;

namespace ReleaseCommunications.Core.UseCases;

public sealed class ReleaseAggregationService(IMrcReleaseClient client) : IReleaseAggregationService
{
    public async Task<AggregatedReleaseResult> GetReleasesAsync(
        ReleaseQuery query,
        CancellationToken cancellationToken)
    {
        var sources = query.Source == ReleaseSource.All
            ? new[] { ReleaseSource.Microsoft365, ReleaseSource.Azure }
            : new[] { query.Source };

        var tasks = sources.Select(async source =>
        {
            try
            {
                return await client.FetchAsync(source, query, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new SourceFetchResult(source, [], ex.Message);
            }
        });

        var results = await Task.WhenAll(tasks);
        var distinct = results
            .SelectMany(result => result.Articles)
            .GroupBy(article => article.StableId, StringComparer.Ordinal)
            .Select(group => group.First());

        return new AggregatedReleaseResult(
            FilterAndSort(distinct, query, DateTimeOffset.UtcNow),
            results);
    }

    public IReadOnlyList<ReleaseArticle> FilterAndSort(
        IEnumerable<ReleaseArticle> articles,
        ReleaseQuery query,
        DateTimeOffset now)
    {
        var filtered = articles.Where(article =>
            (query.Source == ReleaseSource.All || article.Source == query.Source) &&
            Matches(article.Products, query.Product) &&
            Matches(article.Status, query.Status) &&
            Matches(article.Tags, query.Tag));

        if (query.PublishedWithinDays is > 0)
        {
            var threshold = now.AddDays(-query.PublishedWithinDays.Value);
            filtered = filtered.Where(article =>
                article.Dates.PublishedAt is not null &&
                article.Dates.PublishedAt >= threshold);
        }

        return filtered
            .Order(ReleaseArticleComparer.Instance)
            .Take(Math.Clamp(query.MaximumItems, 1, 500))
            .ToArray();
    }

    private static bool Matches(IEnumerable<string> values, string? filter) =>
        string.IsNullOrWhiteSpace(filter) ||
        values.Any(value => value.Contains(filter, StringComparison.OrdinalIgnoreCase));

    private static bool Matches(string? value, string? filter) =>
        string.IsNullOrWhiteSpace(filter) ||
        (value?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
}

public sealed class ReleaseArticleComparer : IComparer<ReleaseArticle>
{
    public static ReleaseArticleComparer Instance { get; } = new();

    public int Compare(ReleaseArticle? x, ReleaseArticle? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return 1;
        }

        if (y is null)
        {
            return -1;
        }

        var xDate = x.Dates.PublishedAt;
        var yDate = y.Dates.PublishedAt;
        if (xDate.HasValue != yDate.HasValue)
        {
            return xDate.HasValue ? -1 : 1;
        }

        var dateComparison = Nullable.Compare(yDate, xDate);
        if (dateComparison != 0)
        {
            return dateComparison;
        }

        var sourceComparison = x.Source.CompareTo(y.Source);
        return sourceComparison != 0
            ? sourceComparison
            : StringComparer.Ordinal.Compare(x.StableId, y.StableId);
    }
}
