using CommunityToolkit.Mvvm.ComponentModel;
using ReleaseCommunications.App.Localization;
using ReleaseCommunications.Core.Models;

namespace ReleaseCommunications.App.ViewModels;

public partial class ArticleItemViewModel(
    ReleaseArticle article,
    AppLocalization localization) : ObservableObject
{
    public ReleaseArticle Article { get; } = article;

    public string StableId => Article.StableId;
    public string Title => Article.Title;
    public string SourceDisplay => Article.Source == ReleaseSource.Azure ? "Azure" : "Microsoft 365";
    public string ProductsDisplay => Article.Products.Count == 0 ? localization.Get("Unknown") : string.Join(", ", Article.Products);
    public string TagsDisplay => Article.Tags.Count == 0 ? localization.Get("None") : string.Join(", ", Article.Tags);
    public string StatusDisplay => Article.Status ?? localization.Get("Unknown");
    public string UpdatedDisplay =>
        Article.Dates.LastUpdatedAt?.ToLocalTime().ToString("g", localization.Culture) ?? localization.Get("Unknown");
    public string PublishedDisplay =>
        Article.Dates.PublishedAt?.ToLocalTime().ToString("g", localization.Culture) ?? localization.Get("Unknown");
    public string SummaryStatusDisplay => localization.SummaryStateName(SummaryState);
    public string? SummaryContentNotice =>
        Summary?.ContentDiagnostics.Count > 0
            ? string.Join(
                Environment.NewLine,
                Summary.ContentDiagnostics.Select(localization.LocalizeDiagnostic))
            : null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryStatusDisplay))]
    public partial SummaryState SummaryState { get; set; } = article.SummaryState;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryContentNotice))]
    public partial ArticleSummary? Summary { get; set; } = article.Summary;

    [ObservableProperty]
    public partial string? SummaryError { get; set; } = article.SummaryError;

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(ProductsDisplay));
        OnPropertyChanged(nameof(TagsDisplay));
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(UpdatedDisplay));
        OnPropertyChanged(nameof(PublishedDisplay));
        OnPropertyChanged(nameof(SummaryStatusDisplay));
    }
}
