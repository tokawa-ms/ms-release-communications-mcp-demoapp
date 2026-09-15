using ReleaseCommunications.Core.Models;
using ReleaseCommunications.Infrastructure.Caching;
using ReleaseCommunications.Infrastructure.Configuration;

namespace ReleaseCommunications.Infrastructure.Tests;

[TestClass]
public sealed class PersistenceTests
{
    private string _directory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ReleaseCommunicationsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task SettingsStore_RoundTripsNonSecretConfiguration()
    {
        using var store = new JsonSettingsStore(_directory);
        var expected = AppSettings.Default with
        {
            FoundryProjectEndpoint = "https://resource.services.ai.azure.com/openai/v1",
            FoundryDeploymentName = "deployment",
            DefaultSummaryLanguage = SummaryLanguage.Japanese,
            UiLanguage = UiLanguage.English
        };

        await store.SaveAsync(expected, CancellationToken.None);
        var actual = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(expected, actual);
        var raw = await File.ReadAllTextAsync(Path.Combine(_directory, "settings.json"));
        Assert.IsFalse(raw.Contains("token", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(raw.Contains("apiKey", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task SettingsStore_AllowsFoundryToRemainUnconfigured()
    {
        using var store = new JsonSettingsStore(_directory);

        await store.SaveAsync(AppSettings.Default, CancellationToken.None);
        var actual = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(string.Empty, actual.FoundryProjectEndpoint);
        Assert.AreEqual(string.Empty, actual.FoundryDeploymentName);
    }

    [TestMethod]
    public async Task SettingsStore_RejectsImageGenerationDeployment()
    {
        using var store = new JsonSettingsStore(_directory);
        var settings = AppSettings.Default with
        {
            FoundryProjectEndpoint = "https://resource-name.services.ai.azure.com/openai/v1",
            FoundryDeploymentName = "gpt-image-example"
        };

        var exception = await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => store.SaveAsync(settings, CancellationToken.None));

        StringAssert.Contains(exception.Message, "image-generation");
        Assert.IsFalse(File.Exists(Path.Combine(_directory, "settings.json")));
    }

    [TestMethod]
    public async Task SummaryCache_RoundTripsSummary()
    {
        using var cache = new JsonSummaryCache(_directory);
        var summary = new ArticleSummary(
            "Overview",
            ["Point"],
            "Impact",
            "Unknown",
            SummaryLanguage.English,
            DateTimeOffset.UtcNow);

        await cache.SetAsync("key", summary, CancellationToken.None);
        var actual = await cache.TryGetAsync("key", CancellationToken.None);

        Assert.IsNotNull(actual);
        Assert.AreEqual(summary.Overview, actual.Overview);
        CollectionAssert.AreEqual(summary.KeyPoints.ToArray(), actual.KeyPoints.ToArray());
        Assert.AreEqual(summary.Impact, actual.Impact);
        Assert.AreEqual(summary.ImportantDates, actual.ImportantDates);
        Assert.AreEqual(summary.Language, actual.Language);
    }
}
