using Microsoft.Extensions.DependencyInjection;
using ReleaseCommunications.Core.Abstractions;
using ReleaseCommunications.Core.Models;
using ReleaseCommunications.Infrastructure.DependencyInjection;

namespace ReleaseCommunications.Infrastructure.Tests;

[TestClass]
public sealed class LiveMrcTests
{
    [TestMethod]
    [TestCategory("Live")]
    public async Task LiveServerReturnsBothSourcesWhenExplicitlyEnabled()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("RUN_LIVE_MRC_TESTS"),
            "1",
            StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set RUN_LIVE_MRC_TESTS=1 to call the public MRC endpoint.");
        }

        var services = new ServiceCollection()
            .AddLogging()
            .AddReleaseCommunications()
            .BuildServiceProvider();
        await using var provider = services;
        var client = provider.GetRequiredService<IMrcReleaseClient>();
        var query = new ReleaseQuery(ReleaseSource.All, 2, PublishedWithinDays: 90);

        var microsoft365 = await client.FetchAsync(
            ReleaseSource.Microsoft365,
            query,
            CancellationToken.None);
        var azure = await client.FetchAsync(
            ReleaseSource.Azure,
            query,
            CancellationToken.None);

        Assert.IsNotEmpty(microsoft365.Articles);
        Assert.IsNotEmpty(azure.Articles);
        Assert.IsTrue(microsoft365.Articles.All(article =>
            article.Dates.PublishedAt >= DateTimeOffset.UtcNow.AddDays(-91)));
        Assert.IsTrue(azure.Articles.All(article =>
            article.Dates.PublishedAt >= DateTimeOffset.UtcNow.AddDays(-91)));
        Assert.IsTrue(IsPublishedDescending(microsoft365.Articles));
        Assert.IsTrue(IsPublishedDescending(azure.Articles));
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task LiveEnrichmentReportsLinkedAndMrcOnlyContentWhenExplicitlyEnabled()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("RUN_LIVE_MRC_TESTS"),
            "1",
            StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set RUN_LIVE_MRC_TESTS=1 to call the public MRC endpoint.");
        }

        var services = new ServiceCollection()
            .AddLogging()
            .AddReleaseCommunications()
            .BuildServiceProvider();
        await using var provider = services;
        var enrichment = provider.GetRequiredService<IArticleEnrichmentService>();

        var microsoft365 = await enrichment.EnrichAsync(
            Placeholder(
                "68688",
                ReleaseSource.Microsoft365,
                new Uri("https://docs.microsoft.com/en-us/microsoft-365/compliance/communication-compliance")),
            CancellationToken.None);
        var azure = await enrichment.EnrichAsync(
            Placeholder(
                "101-services-at-fedramp-high-as-azure-government-continues-expanding-capacity",
                ReleaseSource.Azure,
                null),
            CancellationToken.None);

        Console.WriteLine(
            $"M365: {microsoft365.ContentStatus}; {string.Join(" | ", microsoft365.Diagnostics)}");
        Console.WriteLine(
            $"Azure: {azure.ContentStatus}; {string.Join(" | ", azure.Diagnostics)}");
        Assert.AreEqual(
            ArticleContentStatus.FullLinkedArticle,
            microsoft365.ContentStatus,
            string.Join(" | ", microsoft365.Diagnostics));
        Assert.AreEqual(ArticleContentStatus.MrcDetailOnly, azure.ContentStatus);
        Assert.IsTrue(azure.Diagnostics.Any(value =>
            value.Contains("did not provide a linked source URL", StringComparison.Ordinal)));
    }

    private static ReleaseArticle Placeholder(
        string id,
        ReleaseSource source,
        Uri? sourceUri) =>
        new(
            $"{(source == ReleaseSource.Azure ? "azure" : "m365")}:{id}",
            id,
            source,
            "Placeholder",
            "Truncated...",
            [],
            null,
            [],
            new ReleaseDateInfo(null, null, null, null),
            sourceUri,
            true);

    private static bool IsPublishedDescending(IReadOnlyList<ReleaseArticle> articles) =>
        articles.Zip(articles.Skip(1), (current, next) =>
                current.Dates.PublishedAt >= next.Dates.PublishedAt)
            .All(result => result);
}
