using ReleaseCommunications.Core.Models;
using ReleaseCommunications.Core.UseCases;
using ReleaseCommunications.Core.Validation;

namespace ReleaseCommunications.Core.Tests;

[TestClass]
public sealed class SummarySupportTests
{
    [TestMethod]
    public void BuildPrompt_UsesRequestedLanguageAndGroundedFields()
    {
        var article = CreateArticle();
        var enriched = Enrich(article, "Full linked article text.");

        var japanese = SummaryPromptBuilder.Build(enriched, SummaryLanguage.Japanese);
        var english = SummaryPromptBuilder.Build(enriched, SummaryLanguage.English);

        StringAssert.Contains(japanese, "Respond only in Japanese.");
        StringAssert.Contains(english, "Respond only in English.");
        StringAssert.Contains(japanese, article.Title);
        StringAssert.Contains(japanese, article.Description);
        StringAssert.Contains(japanese, "Full linked article text.");
        StringAssert.Contains(japanese, "Authoritative MRC metadata:");
        StringAssert.Contains(japanese, "Content completeness: FullLinkedArticle");
    }

    [TestMethod]
    public void Parse_AcceptsStrictJsonAndCodeFence()
    {
        const string payload = """
            ```json
            {"overview":"Overview","keyPoints":["One","Two"],"impact":"Impact","importantDates":"Unknown"}
            ```
            """;

        var summary = SummaryResponseParser.Parse(
            payload,
            SummaryLanguage.English,
            DateTimeOffset.UnixEpoch);

        Assert.AreEqual("Overview", summary.Overview);
        Assert.HasCount(2, summary.KeyPoints);
        Assert.AreEqual(DateTimeOffset.UnixEpoch, summary.GeneratedAt);
    }

    [TestMethod]
    public void Parse_RejectsEmptyStructuredFields()
    {
        const string payload =
            """{"overview":"","keyPoints":[],"impact":"Impact","importantDates":"Unknown"}""";

        Assert.ThrowsExactly<FormatException>(() =>
            SummaryResponseParser.Parse(payload, SummaryLanguage.English, DateTimeOffset.UtcNow));
    }

    [TestMethod]
    public void CacheKey_ChangesByLanguageDeploymentAndModifiedDate()
    {
        var article = CreateArticle();
        var enriched = Enrich(article, "Page version one");
        var english = SummaryCacheKey.Create(enriched, SummaryLanguage.English, "deployment-a");
        var japanese = SummaryCacheKey.Create(enriched, SummaryLanguage.Japanese, "deployment-a");
        var otherDeployment = SummaryCacheKey.Create(enriched, SummaryLanguage.English, "deployment-b");
        var modified = SummaryCacheKey.Create(
            Enrich(
                article with
                {
                    Dates = article.Dates with { LastUpdatedAt = article.Dates.LastUpdatedAt?.AddDays(1) }
                },
                "Page version one"),
            SummaryLanguage.English,
            "deployment-a");
        var changedPage = SummaryCacheKey.Create(
            enriched with
            {
                LinkedPageText = "Page version two"
            },
            SummaryLanguage.English,
            "deployment-a");

        Assert.AreNotEqual(english, japanese);
        Assert.AreNotEqual(english, otherDeployment);
        Assert.AreNotEqual(english, modified);
        Assert.AreNotEqual(english, changedPage);
    }

    [TestMethod]
    public void DefaultSettings_UseJapaneseSummaryLanguage()
    {
        Assert.AreEqual(SummaryLanguage.Japanese, AppSettings.Default.DefaultSummaryLanguage);
        Assert.AreEqual(UiLanguage.Japanese, AppSettings.Default.UiLanguage);
    }

    [TestMethod]
    public void BuildPrompt_ExplicitlyMarksUnavailableLinkedContent()
    {
        var article = CreateArticle();
        var enriched = new EnrichedReleaseArticle(
            article,
            article,
            article.SourceUris,
            null,
            null,
            ArticleContentStatus.MrcDetailOnly,
            ["MRC detail did not provide a linked source URL."]);

        var prompt = SummaryPromptBuilder.Build(enriched, SummaryLanguage.Japanese);

        StringAssert.Contains(prompt, "Unavailable. Do not imply that the linked Microsoft page was read.");
        StringAssert.Contains(prompt, "MRC detail did not provide a linked source URL.");
    }

    [TestMethod]
    public void SettingsValidator_AllowsMrcOnlySettingsAndRejectsOutOfRangeValues()
    {
        var settings = AppSettings.Default with
        {
            SummaryConcurrency = 99
        };

        var errors = SettingsValidator.Validate(settings);

        Assert.HasCount(1, errors);
        StringAssert.Contains(errors[0], "Summary concurrency");
    }

    [TestMethod]
    public void FoundryValidator_AcceptsInferenceEndpointButRejectsImageDeployment()
    {
        var settings = AppSettings.Default with
        {
            FoundryProjectEndpoint = "https://resource-name.services.ai.azure.com/openai/v1",
            FoundryDeploymentName = "gpt-image-example"
        };

        var errors = SettingsValidator.ValidateFoundry(settings);

        Assert.HasCount(1, errors);
        Assert.IsTrue(errors.Any(error => error.Contains("image-generation", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void StorageValidator_AllowsBlankFoundryButRejectsPartialConfiguration()
    {
        Assert.IsEmpty(SettingsValidator.ValidateForStorage(AppSettings.Default));

        var errors = SettingsValidator.ValidateForStorage(
            AppSettings.Default with { FoundryDeploymentName = "text-deployment" });

        Assert.IsTrue(errors.Any(error => error.Contains("OpenAI v1 endpoint", StringComparison.Ordinal)));
    }

    private static ReleaseArticle CreateArticle() =>
        new(
            "m365:123",
            "123",
            ReleaseSource.Microsoft365,
            "Feature title",
            "Feature description",
            ["Microsoft Teams"],
            "Rolling out",
            ["Web"],
            new ReleaseDateInfo(
                new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
                "2026-10",
                null),
            new Uri("https://example.test/article"),
            false);

    private static EnrichedReleaseArticle Enrich(ReleaseArticle article, string pageText) =>
        new(
            article,
            article,
            article.SourceUris,
            article.SourceUri,
            pageText,
            ArticleContentStatus.FullLinkedArticle,
            ["Read linked Microsoft article content from learn.microsoft.com."]);
}
