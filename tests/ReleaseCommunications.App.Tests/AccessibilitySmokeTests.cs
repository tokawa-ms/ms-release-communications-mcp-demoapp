using System.Xml.Linq;

namespace ReleaseCommunications.App.Tests;

[TestClass]
public sealed class AccessibilitySmokeTests
{
    [TestMethod]
    public void MainPage_DeclaresStableAutomationIdsForPrimaryInteractions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "MainPage.xaml");
        var document = XDocument.Load(path);
        var ids = document
            .Descendants()
            .Attributes()
            .Where(attribute =>
                attribute.Name.LocalName.Equals(
                    "AutomationProperties.AutomationId",
                    StringComparison.Ordinal))
            .Select(attribute => attribute.Value)
            .ToHashSet(StringComparer.Ordinal);

        var expected = new[]
        {
            "PrimaryNavigation",
            "DashboardNavigationItem",
            "RefreshButton",
            "CancelButton",
            "SourceFilter",
            "PeriodFilter",
            "ProductFilter",
            "StatusFilter",
            "TagFilter",
            "ApplyFiltersButton",
            "ArticleList",
            "SummaryLanguage",
            "UiLanguageComboBox",
            "DefaultSummaryLanguageComboBox",
            "SummarizeSelectedButton",
            "SummarizeVisibleButton",
            "OpenSourceArticleButton",
            "FoundryEndpointTextBox",
            "FoundryDeploymentTextBox",
            "SaveSettingsButton",
            "CheckConnectionsButton"
        };

        foreach (var expectedId in expected)
        {
            Assert.Contains(expectedId, ids);
        }
    }

    [TestMethod]
    public void MainPage_KeepsRetrievalStatusVisibleAndObservesArticleCollection()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "MainPage.xaml");
        var document = XDocument.Load(path);
        var statusInfoBar = FindByAutomationId(document, "StatusInfoBar");
        var articleList = FindByAutomationId(document, "ArticleList");

        Assert.AreEqual("True", statusInfoBar.Attribute("IsOpen")?.Value);
        StringAssert.Contains(
            articleList.Attribute("ItemsSource")?.Value,
            "Mode=OneWay");
    }

    [TestMethod]
    public void MainPage_OriginalArticleButtonUsesExplicitLaunchHandler()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "MainPage.xaml");
        var document = XDocument.Load(path);
        var button = FindByAutomationId(document, "OpenSourceArticleButton");

        Assert.AreEqual("OpenSourceArticle_Click", button.Attribute("Click")?.Value);
        Assert.IsNull(button.Attribute("NavigateUri"));
    }

    private static XElement FindByAutomationId(XDocument document, string automationId) =>
        document.Descendants().Single(element =>
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName.Equals(
                    "AutomationProperties.AutomationId",
                    StringComparison.Ordinal) &&
                attribute.Value.Equals(automationId, StringComparison.Ordinal)));
}
