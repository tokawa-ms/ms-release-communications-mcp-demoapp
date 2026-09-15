using ReleaseCommunications.Core.Abstractions;
using ReleaseCommunications.Core.Models;
using ReleaseCommunications.Core.UseCases;

namespace ReleaseCommunications.Core.Tests;

[TestClass]
public sealed class ReleaseAggregationServiceTests
{
    [TestMethod]
    public void FilterAndSort_UsesPublishedDescendingAndPlacesUnknownLast()
    {
        var service = new ReleaseAggregationService(new StubClient([]));
        var newest = Article("m365:2", ReleaseSource.Microsoft365, new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero));
        var older = Article("azure:1", ReleaseSource.Azure, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        var unknown = Article("azure:3", ReleaseSource.Azure, null);

        var result = service.FilterAndSort(
            [unknown, older, newest],
            new ReleaseQuery(ReleaseSource.All, 50),
            DateTimeOffset.UtcNow);

        CollectionAssert.AreEqual(
            new[] { "m365:2", "azure:1", "azure:3" },
            result.Select(article => article.StableId).ToArray());
    }

    [TestMethod]
    public void FilterAndSort_UsesSourceAndStableIdAsTieBreakers()
    {
        var service = new ReleaseAggregationService(new StubClient([]));
        var date = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var items = new[]
        {
            Article("m365:z", ReleaseSource.Microsoft365, date),
            Article("azure:z", ReleaseSource.Azure, date),
            Article("azure:a", ReleaseSource.Azure, date)
        };

        var result = service.FilterAndSort(
            items,
            new ReleaseQuery(ReleaseSource.All, 50),
            DateTimeOffset.UtcNow);

        CollectionAssert.AreEqual(
            new[] { "m365:z", "azure:a", "azure:z" },
            result.Select(article => article.StableId).ToArray());
    }

    [TestMethod]
    public async Task GetReleasesAsync_PreservesSuccessfulSourceDuringPartialFailure()
    {
        var client = new StubClient(
            [Article("m365:1", ReleaseSource.Microsoft365, DateTimeOffset.UtcNow)],
            failAzure: true);
        var service = new ReleaseAggregationService(client);

        var result = await service.GetReleasesAsync(
            new ReleaseQuery(ReleaseSource.All, 50),
            CancellationToken.None);

        Assert.IsTrue(result.IsPartialFailure);
        Assert.HasCount(1, result.Articles);
        Assert.AreEqual("m365:1", result.Articles[0].StableId);
    }

    [TestMethod]
    public void FilterAndSort_AppliesProductStatusAndTagFilters()
    {
        var service = new ReleaseAggregationService(new StubClient([]));
        var matching = Article("azure:1", ReleaseSource.Azure, DateTimeOffset.UtcNow) with
        {
            Products = ["Azure Storage"],
            Status = "Launched",
            Tags = ["Security"]
        };
        var other = Article("azure:2", ReleaseSource.Azure, DateTimeOffset.UtcNow);

        var result = service.FilterAndSort(
            [matching, other],
            new ReleaseQuery(ReleaseSource.Azure, 50, Product: "storage", Status: "launch", Tag: "security"),
            DateTimeOffset.UtcNow);

        Assert.HasCount(1, result);
        Assert.AreEqual(matching.StableId, result[0].StableId);
    }

    private static ReleaseArticle Article(
        string id,
        ReleaseSource source,
        DateTimeOffset? modified) =>
        new(
            id,
            id.Split(':')[1],
            source,
            id,
            "Description",
            [],
            null,
            [],
            new ReleaseDateInfo(modified, modified, null, null),
            null,
            false);

    private sealed class StubClient(
        IReadOnlyList<ReleaseArticle> microsoft365,
        bool failAzure = false) : IMrcReleaseClient
    {
        public Task<SourceFetchResult> FetchAsync(
            ReleaseSource source,
            ReleaseQuery query,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(source == ReleaseSource.Azure && failAzure
                ? throw new InvalidOperationException("Azure failed")
                : new SourceFetchResult(source, source == ReleaseSource.Microsoft365 ? microsoft365 : []));
        }

        public Task<ConnectionDiagnostic> CheckConnectionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ConnectionDiagnostic("MRC", true, "OK"));

        public Task<ReleaseArticle> FetchDetailAsync(
            ReleaseArticle article,
            CancellationToken cancellationToken) =>
            Task.FromResult(article);
    }
}
