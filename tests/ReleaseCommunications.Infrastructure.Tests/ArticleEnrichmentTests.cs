using ReleaseCommunications.Core.Abstractions;
using ReleaseCommunications.Core.Models;
using ReleaseCommunications.Infrastructure.Articles;

namespace ReleaseCommunications.Infrastructure.Tests;

[TestClass]
public sealed class ArticleEnrichmentTests
{
    [TestMethod]
    [DataRow("https://learn.microsoft.com/en-us/azure/test", true)]
    [DataRow("https://subdomain.microsoft.com/article", true)]
    [DataRow("https://aka.ms/example", true)]
    [DataRow("http://learn.microsoft.com/article", false)]
    [DataRow("https://microsoft.com.evil.example/article", false)]
    [DataRow("https://learn.microsoft.com:8443/article", false)]
    [DataRow("https://example.com/article", false)]
    public void UrlPolicy_AllowsOnlyTrustedMicrosoftHttpsUrls(string value, bool expected)
    {
        Assert.AreEqual(expected, MicrosoftArticleUrlPolicy.IsAllowed(new Uri(value)));
    }

    [TestMethod]
    public async Task HtmlExtractor_SelectsMeaningfulMainContentAndRemovesChrome()
    {
        const string html = """
            <html><body>
              <nav>Navigation text that must not be included</nav>
              <main>
                <h1>Article title</h1>
                <p>This is the first meaningful paragraph with enough text for extraction.</p>
                <p>This is the second meaningful paragraph with customer impact details.</p>
              </main>
              <footer>Footer text that must not be included</footer>
              <script>secret()</script>
            </body></html>
            """;

        var text = await HtmlArticleTextExtractor.ExtractAsync(html, CancellationToken.None);

        StringAssert.Contains(text, "Article title");
        StringAssert.Contains(text, "customer impact details");
        Assert.IsFalse(text.Contains("Navigation text", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("Footer text", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("secret()", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task HtmlExtractor_BoundsLargeContent()
    {
        var html = $"<main><p>{new string('x', HtmlArticleTextExtractor.MaximumTextLength + 2_000)}</p></main>";

        var text = await HtmlArticleTextExtractor.ExtractAsync(html, CancellationToken.None);

        Assert.IsLessThanOrEqualTo(HtmlArticleTextExtractor.MaximumTextLength, text.Length);
        StringAssert.Contains(text, "[Content truncated");
    }

    [TestMethod]
    public async Task Enrichment_UsesDetailAndLinkedPageWhenAvailable()
    {
        var listed = Article("Short...", new Uri("https://learn.microsoft.com/article"));
        var detail = Article("Complete MRC description", listed.SourceUri);
        var service = new ArticleEnrichmentService(
            new StubMrcClient(detail),
            new StubContentClient(
                new ArticlePageContent(
                    true,
                    listed.SourceUri!,
                    listed.SourceUri,
                    "Complete linked article text",
                    "Linked page fetched.")));

        var result = await service.EnrichAsync(listed, CancellationToken.None);

        Assert.AreEqual(ArticleContentStatus.FullLinkedArticle, result.ContentStatus);
        Assert.AreEqual("Complete MRC description", result.AuthoritativeArticle.Description);
        Assert.AreEqual("Complete linked article text", result.LinkedPageText);
    }

    [TestMethod]
    public async Task Enrichment_ReportsPartialContentWhenPageFetchFails()
    {
        var listed = Article("Short...", new Uri("https://learn.microsoft.com/article"));
        var detail = Article("Complete MRC description", listed.SourceUri);
        var service = new ArticleEnrichmentService(
            new StubMrcClient(detail),
            new StubContentClient(
                new ArticlePageContent(
                    false,
                    listed.SourceUri!,
                    null,
                    null,
                    "The linked Microsoft page returned HTTP 403.")));

        var result = await service.EnrichAsync(listed, CancellationToken.None);

        Assert.AreEqual(ArticleContentStatus.MrcDetailOnly, result.ContentStatus);
        Assert.IsNull(result.LinkedPageText);
        Assert.IsTrue(result.Diagnostics.Any(value => value.Contains("HTTP 403", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task Enrichment_FallsBackToListDescriptionWhenDetailFails()
    {
        var listed = Article("List description", null);
        var service = new ArticleEnrichmentService(
            new StubMrcClient(new InvalidOperationException("Detail unavailable")),
            new StubContentClient(null));

        var result = await service.EnrichAsync(listed, CancellationToken.None);

        Assert.AreEqual(ArticleContentStatus.ListDescriptionOnly, result.ContentStatus);
        Assert.AreSame(listed, result.AuthoritativeArticle);
        Assert.IsTrue(result.Diagnostics.Any(value =>
            value.Contains("Detail unavailable", StringComparison.Ordinal)));
    }

    private static ReleaseArticle Article(string description, Uri? uri) =>
        new(
            "m365:123",
            "123",
            ReleaseSource.Microsoft365,
            "Title",
            description,
            ["Microsoft Teams"],
            "Rolling out",
            ["Web"],
            new ReleaseDateInfo(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "2026-10", null),
            uri,
            description.EndsWith("...", StringComparison.Ordinal));

    private sealed class StubMrcClient : IMrcReleaseClient
    {
        private readonly ReleaseArticle? _detail;
        private readonly Exception? _exception;

        public StubMrcClient(ReleaseArticle detail) => _detail = detail;
        public StubMrcClient(Exception exception) => _exception = exception;

        public Task<ReleaseArticle> FetchDetailAsync(
            ReleaseArticle article,
            CancellationToken cancellationToken) =>
            _exception is null
                ? Task.FromResult(_detail!)
                : Task.FromException<ReleaseArticle>(_exception);

        public Task<SourceFetchResult> FetchAsync(
            ReleaseSource source,
            ReleaseQuery query,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ConnectionDiagnostic> CheckConnectionAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubContentClient(ArticlePageContent? result) : IArticleContentClient
    {
        public Task<ArticlePageContent> FetchAsync(
            Uri uri,
            CancellationToken cancellationToken) =>
            Task.FromResult(result ?? throw new AssertFailedException("Content fetch was not expected."));
    }
}
