using ReleaseCommunications.Core.Models;
using ReleaseCommunications.Infrastructure.Mrc;

namespace ReleaseCommunications.Infrastructure.Tests;

[TestClass]
public sealed class MrcPayloadParserTests
{
    [TestMethod]
    public void ParsePage_MapsModifiedAndCreatedWithoutSubstitution()
    {
        const string json = """
            {
              "items": [{
                "id": 65632,
                "title": "Excel update",
                "description": "Description",
                "products": ["Excel"],
                "status": "Launched",
                "created": "2026-08-01T00:00:00Z",
                "modified": "2026-09-01T12:30:00Z",
                "generalAvailabilityDate": "2026-09",
                "moreInfoUrls": ["https://example.test/65632"]
              }],
              "offset": 0,
              "limit": 50,
              "totalCount": 1,
              "hasMore": false
            }
            """;

        var page = MrcPayloadParser.ParsePage(json, ReleaseSource.Microsoft365);
        var article = page.Articles[0];

        Assert.AreEqual("m365:65632", article.StableId);
        Assert.AreEqual(
            new DateTimeOffset(2026, 9, 1, 12, 30, 0, TimeSpan.Zero),
            article.Dates.LastUpdatedAt);
        Assert.AreEqual(
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            article.Dates.PublishedAt);
    }

    [TestMethod]
    public void ParsePage_DoesNotUseCreatedWhenModifiedIsMissing()
    {
        const string json = """
            {
              "items": [{
                "id": "467931",
                "title": "Azure update",
                "description": "Description",
                "products": ["Azure Storage"],
                "created": "2026-08-01T00:00:00Z"
              }]
            }
            """;

        var article = MrcPayloadParser.ParsePage(json, ReleaseSource.Azure).Articles[0];

        Assert.IsNull(article.Dates.LastUpdatedAt);
        Assert.IsNotNull(article.Dates.PublishedAt);
    }

    [TestMethod]
    public void ParsePage_ThrowsForInvalidModifiedDate()
    {
        const string json = """
            {"items":[{"id":"1","title":"Title","description":"Text","modified":"not-a-date"}]}
            """;

        Assert.ThrowsExactly<FormatException>(() =>
            MrcPayloadParser.ParsePage(json, ReleaseSource.Azure));
    }

    [TestMethod]
    public void ParsePage_CombinesAzureTagsAndProductCategories()
    {
        const string json = """
            {
              "items": [{
                "id": "1",
                "title": "Title",
                "description": "Text",
                "tags": ["Security"],
                "productCategories": ["Compute"]
              }]
            }
            """;

        var article = MrcPayloadParser.ParsePage(json, ReleaseSource.Azure).Articles[0];

        CollectionAssert.AreEquivalent(new[] { "Security", "Compute" }, article.Tags.ToArray());
    }

    [TestMethod]
    public void ParseDetail_PreservesAllSourceUrls()
    {
        const string json = """
            {
              "id": "42",
              "title": "Title",
              "description": "Complete description",
              "moreInfoUrl": "https://learn.microsoft.com/first",
              "moreInfoUrls": [
                "https://support.microsoft.com/second",
                "https://learn.microsoft.com/first"
              ]
            }
            """;

        var article = MrcPayloadParser.ParseDetail(json, ReleaseSource.Microsoft365);

        Assert.HasCount(2, article.SourceUris);
        Assert.AreEqual("https://learn.microsoft.com/first", article.SourceUri?.AbsoluteUri);
    }
}
