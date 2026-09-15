using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReleaseCommunications.App.Localization;
using ReleaseCommunications.Core.Abstractions;
using ReleaseCommunications.Core.Models;
using ReleaseCommunications.Core.Validation;

namespace ReleaseCommunications.App.ViewModels;

public sealed record ChoiceOption<T>(T Value, string Name) where T : struct, Enum;
public sealed record PeriodOption(string Name, int? Days);

public partial class MainPageViewModel(
    IReleaseAggregationService aggregationService,
    IMrcReleaseClient mrcClient,
    IArticleEnrichmentService enrichmentService,
    ISummarizationService summarizationService,
    ISettingsStore settingsStore,
    ISummaryCache summaryCache,
    AppLocalization localization) : ObservableObject, IDisposable
{
    private static readonly int[] PeriodDays = [90, 30, 7, 365];
    private readonly List<ReleaseArticle> _allArticles = [];
    private CancellationTokenSource? _operationCancellation;
    private bool _isInitialized;

    public ObservableCollection<ArticleItemViewModel> Articles { get; } = [];
    public ObservableCollection<ChoiceOption<ReleaseSource>> SourceOptions { get; } = [];
    public ObservableCollection<ChoiceOption<SummaryLanguage>> SummaryLanguageOptions { get; } = [];
    public ObservableCollection<ChoiceOption<UiLanguage>> UiLanguageOptions { get; } = [];
    public ObservableCollection<PeriodOption> PeriodOptions { get; } = [];

    public event EventHandler? UiLanguageChanged;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(SummarizeSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(SummarizeVisibleCommand))]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial LoadState State { get; set; } = LoadState.Idle;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = localization.Get("ReadyStatus");

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SummarizeSelectedCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial ArticleItemViewModel? SelectedArticle { get; set; }

    [ObservableProperty]
    public partial ReleaseSource SelectedSource { get; set; } = ReleaseSource.All;

    [ObservableProperty]
    public partial ChoiceOption<ReleaseSource>? SelectedSourceOption { get; set; }

    [ObservableProperty]
    public partial PeriodOption? SelectedPeriod { get; set; }

    [ObservableProperty]
    public partial string ProductFilter { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusFilter { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TagFilter { get; set; } = string.Empty;

    [ObservableProperty]
    public partial SummaryLanguage SelectedSummaryLanguage { get; set; } = SummaryLanguage.Japanese;

    [ObservableProperty]
    public partial ChoiceOption<SummaryLanguage>? SelectedSummaryLanguageOption { get; set; }

    [ObservableProperty]
    public partial UiLanguage SelectedUiLanguage { get; set; } = UiLanguage.Japanese;

    [ObservableProperty]
    public partial ChoiceOption<UiLanguage>? SelectedUiLanguageOption { get; set; }

    [ObservableProperty]
    public partial bool IsSettingsVisible { get; set; }

    [ObservableProperty]
    public partial string FoundryProjectEndpoint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FoundryDeploymentName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int MaximumItems { get; set; } = 50;

    [ObservableProperty]
    public partial int SummaryConcurrency { get; set; } = 3;

    [ObservableProperty]
    public partial int CacheRetentionDays { get; set; } = 14;

    [ObservableProperty]
    public partial string SettingsStatus { get; set; } = localization.Get("SettingsStored");

    public bool IsDashboardVisible => !IsSettingsVisible;
    public bool CanCancel => IsBusy;
    public bool HasSelection => SelectedArticle is not null;
    public bool HasSelectedSummary => SelectedArticle?.Summary is not null;
    public bool HasSelectedSummaryError => !string.IsNullOrWhiteSpace(SelectedArticle?.SummaryError);

    public string Text(string key) => localization.Get(key);

    partial void OnIsSettingsVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(IsDashboardVisible));

    partial void OnSelectedArticleChanged(ArticleItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedSummary));
        OnPropertyChanged(nameof(HasSelectedSummaryError));
    }

    partial void OnSelectedSourceOptionChanged(ChoiceOption<ReleaseSource>? value)
    {
        if (value is not null)
        {
            SelectedSource = value.Value;
        }
    }

    partial void OnSelectedSummaryLanguageOptionChanged(ChoiceOption<SummaryLanguage>? value)
    {
        if (value is not null)
        {
            SelectedSummaryLanguage = value.Value;
        }
    }

    partial void OnSelectedUiLanguageOptionChanged(ChoiceOption<UiLanguage>? value)
    {
        if (value is not null)
        {
            SelectedUiLanguage = value.Value;
        }
    }

    partial void OnSelectedUiLanguageChanged(UiLanguage value)
    {
        localization.SetLanguage(value);
        RebuildLocalizedOptions();
        foreach (var article in Articles)
        {
            article.RefreshLocalization();
        }

        RefreshLocalizedState();
        SettingsStatus = localization.Get("SettingsStored");
        UiLanguageChanged?.Invoke(this, EventArgs.Empty);
        if (_isInitialized)
        {
            _ = PersistUiLanguageAsync();
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            var settings = await settingsStore.LoadAsync(CancellationToken.None);
            ApplySettings(settings);
            await summaryCache.PruneAsync(settings, CancellationToken.None);
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            SettingsStatus = localization.LocalizeDiagnostic(ex.Message);
        }
    }

    public void ShowDashboard() => IsSettingsVisible = false;
    public void ShowSettings() => IsSettingsVisible = true;

    public async Task<Uri?> ResolveSourceUriAsync()
    {
        if (SelectedArticle is null)
        {
            return null;
        }

        ErrorMessage = null;
        StatusMessage = localization.Get("ResolvingSourceLink");
        try
        {
            var sourceUri = SelectOpenableSourceUri(SelectedArticle.Article.SourceUris);
            if (sourceUri is not null)
            {
                return sourceUri;
            }

            var detail = await mrcClient.FetchDetailAsync(
                SelectedArticle.Article,
                CancellationToken.None);
            sourceUri = SelectOpenableSourceUri(detail.SourceUris);
            if (sourceUri is null)
            {
                ErrorMessage = localization.Get("SourceLinkUnavailable");
                StatusMessage = localization.Get("FailedStatus");
            }

            return sourceUri;
        }
        catch (Exception ex)
        {
            ErrorMessage = localization.LocalizeDiagnostic(ex.Message);
            StatusMessage = localization.Get("FailedStatus");
            return null;
        }
    }

    public void ReportSourceLaunchFailure()
    {
        ErrorMessage = localization.Get("SourceLaunchFailed");
        StatusMessage = localization.Get("FailedStatus");
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        await RunOperationAsync(async cancellationToken =>
        {
            State = LoadState.Loading;
            StatusMessage = localization.Get("RetrievingStatus");
            ErrorMessage = null;

            var result = await aggregationService.GetReleasesAsync(CreateQuery(), cancellationToken);
            _allArticles.Clear();
            _allArticles.AddRange(result.Articles);
            ApplyFilters();

            if (result.IsTotalFailure)
            {
                State = LoadState.Error;
                ErrorMessage = string.Join(
                    Environment.NewLine,
                    result.Sources.Select(source =>
                        $"{localization.SourceName(source.Source)}: {localization.LocalizeDiagnostic(source.Error ?? string.Empty)}"));
                StatusMessage = localization.Get("NoSourceStatus");
            }
            else if (result.IsPartialFailure)
            {
                State = LoadState.PartialFailure;
                ErrorMessage = string.Join(
                    Environment.NewLine,
                    result.Sources.Where(source => source.Error is not null)
                        .Select(source =>
                            $"{localization.SourceName(source.Source)}: {localization.LocalizeDiagnostic(source.Error!)}"));
                StatusMessage = localization.Format("PartialStatus", Articles.Count);
            }
            else if (Articles.Count == 0)
            {
                State = LoadState.Empty;
                StatusMessage = localization.Get("EmptyStatus");
            }
            else
            {
                State = LoadState.Success;
                var sourceCounts = string.Join(
                    ", ",
                    result.Sources.Select(source =>
                        $"{localization.SourceName(source.Source)}: {source.Articles.Count}"));
                StatusMessage = localization.Format("LoadedStatus", Articles.Count, sourceCounts);
            }
        });
    }

    [RelayCommand]
    private void ApplyFilters()
    {
        var selectedId = SelectedArticle?.StableId;
        var filtered = aggregationService.FilterAndSort(
            _allArticles,
            CreateQuery(),
            DateTimeOffset.UtcNow);
        Articles.Clear();
        foreach (var article in filtered)
        {
            Articles.Add(new ArticleItemViewModel(article, localization));
        }

        SelectedArticle = selectedId is null
            ? null
            : Articles.FirstOrDefault(item => item.StableId == selectedId);
        SummarizeVisibleCommand.NotifyCanExecuteChanged();
        if (!IsBusy)
        {
            State = Articles.Count == 0 && _allArticles.Count > 0 ? LoadState.Empty : State;
            StatusMessage = Articles.Count == 0
                ? localization.Get("EmptyStatus")
                : localization.Format("ShowingStatus", Articles.Count);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSummarizeSelected))]
    private async Task SummarizeSelectedAsync()
    {
        if (SelectedArticle is null)
        {
            return;
        }

        await RunOperationAsync(cancellationToken =>
            SummarizeItemAsync(SelectedArticle, cancellationToken));
    }

    [RelayCommand(CanExecute = nameof(CanSummarizeVisible))]
    private async Task SummarizeVisibleAsync()
    {
        await RunOperationAsync(async cancellationToken =>
        {
            var settings = CurrentSettings();
            var validation = SettingsValidator.Validate(settings)
                .Concat(SettingsValidator.ValidateFoundry(settings))
                .Select(localization.LocalizeDiagnostic)
                .ToArray();
            if (validation.Length > 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, validation));
            }

            StatusMessage = localization.Format(
                "SummarizingMany",
                Articles.Count,
                settings.SummaryConcurrency);
            using var semaphore = new SemaphoreSlim(settings.SummaryConcurrency);
            var tasks = Articles.Select(async item =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await SummarizeItemAsync(item, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            StatusMessage = localization.Format("SummaryComplete", Articles.Count);
        });
    }

    [RelayCommand]
    private void Cancel() => _operationCancellation?.Cancel();

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            var settings = CurrentSettings();
            await settingsStore.SaveAsync(settings, CancellationToken.None);
            await summaryCache.PruneAsync(settings, CancellationToken.None);
            SettingsStatus = localization.Get("SettingsSaved");
        }
        catch (Exception ex)
        {
            SettingsStatus = localization.LocalizeDiagnostic(ex.Message);
        }
    }

    [RelayCommand]
    private async Task CheckConnectionsAsync()
    {
        try
        {
            IsBusy = true;
            var mrc = await mrcClient.CheckConnectionAsync(CancellationToken.None);
            var settings = CurrentSettings();
            var foundryValidation = SettingsValidator.ValidateFoundry(settings);
            var foundry = foundryValidation.Count > 0
                ? new ConnectionDiagnostic(
                    "Foundry",
                    false,
                    string.Join(" ", foundryValidation.Select(localization.LocalizeDiagnostic)))
                : await summarizationService.CheckConnectionAsync(settings, CancellationToken.None);
            SettingsStatus =
                $"MRC: {ConnectionResult(mrc)} - {localization.LocalizeDiagnostic(mrc.Message)}{Environment.NewLine}" +
                $"Foundry: {ConnectionResult(foundry)} - {localization.LocalizeDiagnostic(foundry.Message)}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PersistUiLanguageAsync()
    {
        try
        {
            var savedSettings = await settingsStore.LoadAsync(CancellationToken.None);
            await settingsStore.SaveAsync(
                savedSettings with { UiLanguage = SelectedUiLanguage },
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            SettingsStatus = localization.LocalizeDiagnostic(ex.Message);
        }
    }

    private async Task SummarizeItemAsync(
        ArticleItemViewModel item,
        CancellationToken cancellationToken)
    {
        item.SummaryState = SummaryState.InProgress;
        item.SummaryError = null;
        try
        {
            var enriched = await enrichmentService.EnrichAsync(item.Article, cancellationToken);
            item.Summary = await summarizationService.SummarizeAsync(
                enriched,
                SelectedSummaryLanguage,
                cancellationToken);
            item.SummaryState = SummaryState.Completed;
            NotifySelectedSummaryState(item);
        }
        catch (OperationCanceledException)
        {
            item.SummaryState = SummaryState.Cancelled;
            NotifySelectedSummaryState(item);
            throw;
        }
        catch (Exception ex)
        {
            item.SummaryState = SummaryState.Failed;
            item.SummaryError = localization.LocalizeDiagnostic(ex.Message);
            NotifySelectedSummaryState(item);
        }
    }

    private void NotifySelectedSummaryState(ArticleItemViewModel item)
    {
        if (ReferenceEquals(item, SelectedArticle))
        {
            OnPropertyChanged(nameof(HasSelectedSummary));
            OnPropertyChanged(nameof(HasSelectedSummaryError));
        }
    }

    private async Task RunOperationAsync(Func<CancellationToken, Task> operation)
    {
        _operationCancellation?.Dispose();
        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        try
        {
            await operation(_operationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            State = LoadState.Cancelled;
            StatusMessage = localization.Get("CancelledStatus");
        }
        catch (Exception ex)
        {
            State = LoadState.Error;
            ErrorMessage = localization.LocalizeDiagnostic(ex.Message);
            StatusMessage = localization.Get("FailedStatus");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRefresh() => !IsBusy;
    private bool CanSummarizeSelected() => !IsBusy && SelectedArticle is not null;
    private bool CanSummarizeVisible() => !IsBusy && Articles.Count > 0;

    private string ConnectionResult(ConnectionDiagnostic diagnostic) =>
        localization.Get(diagnostic.IsSuccessful ? "ConnectionOk" : "ConnectionFailed");

    private ReleaseQuery CreateQuery() => new(
        SelectedSource,
        MaximumItems,
        SelectedPeriod?.Days,
        ProductFilter,
        StatusFilter,
        TagFilter);

    private AppSettings CurrentSettings() => new(
        FoundryProjectEndpoint.Trim(),
        FoundryDeploymentName.Trim(),
        SelectedSummaryLanguage,
        SelectedSource,
        MaximumItems,
        SummaryConcurrency,
        CacheRetentionDays,
        AppSettings.Default.MaximumCacheEntries,
        SelectedUiLanguage);

    private void ApplySettings(AppSettings settings)
    {
        FoundryProjectEndpoint = settings.FoundryProjectEndpoint;
        FoundryDeploymentName = settings.FoundryDeploymentName;
        SelectedSummaryLanguage = settings.DefaultSummaryLanguage;
        SelectedSource = settings.DefaultSource;
        MaximumItems = settings.MaximumItems;
        SummaryConcurrency = settings.SummaryConcurrency;
        CacheRetentionDays = settings.CacheRetentionDays;
        SelectedUiLanguage = settings.UiLanguage;
        localization.SetLanguage(settings.UiLanguage);
        RebuildLocalizedOptions();
        SelectedPeriod = PeriodOptions.First(option => option.Days == 90);
        RefreshLocalizedState();
    }

    private void RebuildLocalizedOptions()
    {
        var selectedDays = SelectedPeriod?.Days ?? 90;
        Replace(
            SourceOptions,
            Enum.GetValues<ReleaseSource>()
                .Select(value => new ChoiceOption<ReleaseSource>(value, localization.SourceName(value))));
        Replace(
            SummaryLanguageOptions,
            Enum.GetValues<SummaryLanguage>()
                .OrderByDescending(value => value == SummaryLanguage.Japanese)
                .Select(value => new ChoiceOption<SummaryLanguage>(
                    value,
                    localization.SummaryLanguageName(value))));
        Replace(
            UiLanguageOptions,
            Enum.GetValues<UiLanguage>()
                .Select(value => new ChoiceOption<UiLanguage>(
                    value,
                    localization.UiLanguageName(value))));
        Replace(
            PeriodOptions,
            PeriodDays
                .Select(days => new PeriodOption(localization.Format("LastDays", days), days)));
        SelectedSourceOption = SourceOptions.First(option => option.Value == SelectedSource);
        SelectedSummaryLanguageOption =
            SummaryLanguageOptions.First(option => option.Value == SelectedSummaryLanguage);
        SelectedUiLanguageOption =
            UiLanguageOptions.First(option => option.Value == SelectedUiLanguage);
        SelectedPeriod = PeriodOptions.First(option => option.Days == selectedDays);
    }

    private void RefreshLocalizedState()
    {
        StatusMessage = State switch
        {
            LoadState.Loading => localization.Get("RetrievingStatus"),
            LoadState.Empty => localization.Get("EmptyStatus"),
            LoadState.Cancelled => localization.Get("CancelledStatus"),
            LoadState.Error => localization.Get("FailedStatus"),
            LoadState.Success or LoadState.PartialFailure when Articles.Count > 0 =>
                localization.Format("ShowingStatus", Articles.Count),
            _ => localization.Get("ReadyStatus")
        };
        if (!string.IsNullOrWhiteSpace(ErrorMessage))
        {
            ErrorMessage = localization.LocalizeDiagnostic(ErrorMessage);
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private static Uri? SelectOpenableSourceUri(IEnumerable<Uri> sourceUris) =>
        sourceUris.FirstOrDefault(uri =>
            uri.IsAbsoluteUri &&
            uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));

    public void Dispose()
    {
        _operationCancellation?.Dispose();
        _operationCancellation = null;
        GC.SuppressFinalize(this);
    }
}
