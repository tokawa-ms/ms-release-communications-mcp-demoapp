using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReleaseCommunications.App.ViewModels;
using ReleaseCommunications.Core.Models;

namespace ReleaseCommunications.App;

public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; } =
        App.Services.GetRequiredService<MainPageViewModel>();

    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ViewModel.UiLanguageChanged += OnUiLanguageChanged;
    }

    public static Visibility VisibleWhen(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility VisibleWhenNot(bool value) =>
        value ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility VisibleForState(LoadState value, LoadState expected) =>
        value == expected ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility VisibleWhenPresent(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;

    public static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    public static Visibility VisibleWhenSummary(ArticleSummary? value) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ViewModel.InitializeAsync();
        UpdateLocalizedChrome();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.UiLanguageChanged -= OnUiLanguageChanged;
    }

    private void OnUiLanguageChanged(object? sender, EventArgs e) =>
        UpdateLocalizedChrome();

    private void UpdateLocalizedChrome()
    {
        Bindings.Update();
    }

    private void NavigationView_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag?.ToString() == "settings")
        {
            ViewModel.ShowSettings();
            return;
        }

        ViewModel.ShowDashboard();
    }

    private async void OpenSourceArticle_Click(object sender, RoutedEventArgs e)
    {
        var uri = await ViewModel.ResolveSourceUriAsync();
        if (uri is null)
        {
            return;
        }

        if (!await Windows.System.Launcher.LaunchUriAsync(uri))
        {
            ViewModel.ReportSourceLaunchFailure();
        }
    }
}
