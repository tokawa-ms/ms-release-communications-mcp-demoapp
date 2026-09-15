using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using ReleaseCommunications.Core.Models;

namespace ReleaseCommunications.App.Localization;

public sealed partial class AppLocalization : ObservableObject
{
    private static readonly Dictionary<string, (string Japanese, string English)> Strings =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["AppTitle"] = ("リリース コミュニケーション", "Release Communications"),
            ["PrimaryNavigation"] = ("メイン ナビゲーション", "Primary navigation"),
            ["NavUpdates"] = ("更新情報", "Updates"),
            ["Settings"] = ("設定", "Settings"),
            ["PageTitle"] = ("リリース情報", "Release communications"),
            ["PageSubtitle"] = ("Microsoft 365 ロードマップと Azure 更新情報を公開日の新しい順で表示します。要約には MRC の詳細情報と信頼できる Microsoft ページの本文を使用します。", "Microsoft 365 roadmap and Azure Updates, ordered by publication date. Summaries use MRC detail and trusted linked Microsoft pages."),
            ["Refresh"] = ("更新", "Refresh"),
            ["Cancel"] = ("キャンセル", "Cancel"),
            ["Source"] = ("ソース", "Source"),
            ["PublishedPeriod"] = ("公開期間", "Published period"),
            ["Product"] = ("製品", "Product"),
            ["Status"] = ("状態", "Status"),
            ["TagOrPlatform"] = ("タグまたはプラットフォーム", "Tag or platform"),
            ["Apply"] = ("適用", "Apply"),
            ["ProductPlaceholder"] = ("例: Microsoft Teams", "e.g. Microsoft Teams"),
            ["StatusPlaceholder"] = ("例: 提供開始", "e.g. Launched"),
            ["TagPlaceholder"] = ("例: セキュリティ", "e.g. Security"),
            ["NoMatchingArticles"] = ("一致する記事はありません", "No matching articles"),
            ["NoMatchingArticlesHelp"] = ("フィルターを変更するか、データを更新してください。", "Change filters or refresh the data source."),
            ["SummaryLanguage"] = ("要約言語", "Summary language"),
            ["SummarizeSelected"] = ("記事全文を要約", "Summarize full article"),
            ["SummarizeVisible"] = ("表示中の記事を要約", "Summarize visible full articles"),
            ["AiSummary"] = ("AI 要約", "AI summary"),
            ["KeyPoints"] = ("要点", "Key points"),
            ["Impact"] = ("影響", "Impact"),
            ["ImportantDates"] = ("重要な日付", "Important dates"),
            ["OriginalArticle"] = ("元の記事", "Original article"),
            ["Products"] = ("製品", "Products"),
            ["LastUpdated"] = ("最終更新", "Last updated"),
            ["Published"] = ("公開日", "Published"),
            ["OpenOriginal"] = ("元の記事を開く", "Open original article"),
            ["ResolvingSourceLink"] = ("元の記事へのリンクを確認しています...", "Resolving the original article link..."),
            ["SourceLinkUnavailable"] = ("この記事には開くことができる元記事 URL がありません。", "This item does not provide an original article URL that can be opened."),
            ["SourceLaunchFailed"] = ("既定のブラウザーで元の記事を開けませんでした。", "The original article could not be opened in the default browser."),
            ["SettingsIntro"] = ("秘密情報ではない設定のみを保存します。認証には常に Azure CLI セッションを使用します。", "Only non-secret configuration is stored. Authentication always uses your Azure CLI session."),
            ["UiLanguage"] = ("UI の言語", "UI language"),
            ["SummaryLanguageSetting"] = ("既定の要約言語", "Default summary language"),
            ["Endpoint"] = ("Foundry OpenAI v1 エンドポイント", "Foundry OpenAI v1 endpoint"),
            ["Deployment"] = ("モデル デプロイ名", "Model deployment name"),
            ["DeploymentHelp"] = ("テキスト対応のチャットまたは Responses デプロイを使用してください。画像生成デプロイでは記事を要約できません。", "Use a text-capable chat or responses deployment. Image-generation deployments cannot summarize articles."),
            ["MaximumArticles"] = ("記事の最大件数", "Maximum articles"),
            ["SummaryConcurrency"] = ("要約の同時実行数", "Summary concurrency"),
            ["CacheRetention"] = ("キャッシュ保持日数", "Cache retention days"),
            ["Save"] = ("保存", "Save"),
            ["CheckConnections"] = ("接続を確認", "Check connections"),
            ["ApiTerms"] = ("Microsoft API 利用規約", "Microsoft API Terms of Use"),
            ["ReadyStatus"] = ("［更新］を選択してリリース情報を取得してください。", "Choose Refresh to retrieve release communications."),
            ["RetrievingStatus"] = ("Microsoft のリリース情報を取得しています...", "Retrieving Microsoft release communications..."),
            ["NoSourceStatus"] = ("どのソースからも情報を取得できませんでした。", "No source could be loaded."),
            ["PartialStatus"] = ("{0} 件の記事を読み込みました。一部のソースで取得に失敗しました。", "Loaded {0} articles; one source failed."),
            ["EmptyStatus"] = ("現在のフィルターに一致する記事はありません。", "No articles match the current filters."),
            ["LoadedStatus"] = ("{0} 件の記事を読み込みました（{1}）。公開日の新しい順です。", "Loaded {0} articles ({1}), newest publication first."),
            ["ShowingStatus"] = ("{0} 件の記事を表示しています。公開日の新しい順です。", "Showing {0} articles, newest publication first."),
            ["SummarizingMany"] = ("記事全文を取得し、{0} 件を最大 {1} 件ずつ同時に要約しています...", "Retrieving full article content and summarizing {0} articles with up to {1} concurrent requests..."),
            ["SummaryComplete"] = ("{0} 件の記事の要約が完了しました。", "Summary run completed for {0} articles."),
            ["CancelledStatus"] = ("操作をキャンセルしました。取得済みの結果は保持されています。", "Operation cancelled. Existing successful results were preserved."),
            ["FailedStatus"] = ("操作に失敗しました。", "The operation failed."),
            ["SettingsStored"] = ("設定は資格情報やトークンを含めずにローカルへ保存されます。", "Settings are stored locally without credentials or tokens."),
            ["SettingsSaved"] = ("設定を保存しました。資格情報やトークンは保存されていません。", "Settings saved. No credentials or tokens were stored."),
            ["ConnectionOk"] = ("成功", "OK"),
            ["ConnectionFailed"] = ("失敗", "Failed"),
            ["Unknown"] = ("不明", "Unknown"),
            ["None"] = ("なし", "None"),
            ["NotSummarized"] = ("未要約", "Not summarized"),
            ["Summarizing"] = ("要約中...", "Summarizing..."),
            ["Completed"] = ("完了", "Completed"),
            ["Failed"] = ("失敗", "Failed"),
            ["Cancelled"] = ("キャンセル済み", "Cancelled"),
            ["AllSources"] = ("すべて", "All"),
            ["LastDays"] = ("過去 {0} 日間", "Last {0} days"),
            ["Japanese"] = ("日本語", "Japanese"),
            ["English"] = ("英語", "English"),
            ["RefreshAutomation"] = ("リリース記事を更新", "Refresh release articles"),
            ["CancelAutomation"] = ("現在の操作をキャンセル", "Cancel current operation"),
            ["ArticleListAutomation"] = ("リリース記事", "Release articles"),
            ["LoadingAutomation"] = ("リリース情報を読み込み中", "Loading release data"),
            ["OpenOriginalAutomation"] = ("元の記事を開く", "Open original article"),
            ["TrayShow"] = ("ウィンドウを表示", "Show window"),
            ["TrayExit"] = ("終了", "Exit"),
            ["OriginalArtworkNotice"] = ("アプリアイコンと通知領域アイコンは、クラウド、更新、リリースの流れを抽象化した本プロジェクト独自のデザインです。Microsoft のロゴや商標は使用していません。", "The app and notification-area icons are original artwork for this project, using an abstract cloud, update, and release-flow motif. They do not use Microsoft logos or trademarks.")
        };

    [ObservableProperty]
    public partial UiLanguage Language { get; private set; } = UiLanguage.Japanese;

    public AppLocalization()
    {
        ApplyCulture();
    }

    public CultureInfo Culture =>
        Language == UiLanguage.Japanese
            ? CultureInfo.GetCultureInfo("ja-JP")
            : CultureInfo.GetCultureInfo("en-US");

    public event EventHandler? LanguageChanged;

    public string Get(string key)
    {
        var value = Strings.TryGetValue(key, out var localized)
            ? localized
            : (Japanese: key, English: key);
        return Language == UiLanguage.Japanese ? value.Japanese : value.English;
    }

    public string Format(string key, params object?[] arguments) =>
        string.Format(Culture, Get(key), arguments);

    public string SourceName(ReleaseSource source) => source switch
    {
        ReleaseSource.All => Get("AllSources"),
        ReleaseSource.Microsoft365 => "Microsoft 365",
        ReleaseSource.Azure => "Azure",
        _ => source.ToString()
    };

    public string SummaryLanguageName(SummaryLanguage language) =>
        language == SummaryLanguage.Japanese ? Get("Japanese") : Get("English");

    public string UiLanguageName(UiLanguage language) =>
        language == UiLanguage.Japanese ? Get("Japanese") : Get("English");

    public string SummaryStateName(SummaryState state) => state switch
    {
        SummaryState.NotStarted => Get("NotSummarized"),
        SummaryState.InProgress => Get("Summarizing"),
        SummaryState.Completed => Get("Completed"),
        SummaryState.Failed => Get("Failed"),
        SummaryState.Cancelled => Get("Cancelled"),
        _ => state.ToString()
    };

    public string LocalizeDiagnostic(string message)
    {
        if (Language == UiLanguage.English || string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        return message
            .Replace("Maximum items must be between 1 and 500.", "記事の最大件数は 1～500 にしてください。", StringComparison.Ordinal)
            .Replace("Summary concurrency must be between 1 and 8.", "要約の同時実行数は 1～8 にしてください。", StringComparison.Ordinal)
            .Replace("Cache retention must be between 1 and 90 days.", "キャッシュ保持日数は 1～90 日にしてください。", StringComparison.Ordinal)
            .Replace("Foundry OpenAI v1 endpoint must be an absolute HTTPS URI.", "Foundry OpenAI v1 エンドポイントには絶対 HTTPS URI を指定してください。", StringComparison.Ordinal)
            .Replace("Foundry OpenAI v1 endpoint must end in /openai/v1.", "Foundry OpenAI v1 エンドポイントの末尾は /openai/v1 にしてください。", StringComparison.Ordinal)
            .Replace("Foundry deployment name is required.", "Foundry デプロイ名を入力してください。", StringComparison.Ordinal)
            .Replace("Saved settings are corrupted. Delete settings.json or save new settings.", "保存済みの設定が破損しています。settings.json を削除するか、新しい設定を保存してください。", StringComparison.Ordinal)
            .Replace("MRC detail did not provide a linked source URL.", "MRC 詳細にリンク先 URL がありませんでした。", StringComparison.Ordinal)
            .Replace("MRC detail did not provide a trusted Microsoft HTTPS article URL.", "MRC 詳細に信頼できる Microsoft HTTPS 記事 URL がありませんでした。", StringComparison.Ordinal)
            .Replace("MRC detail retrieval failed; the list description is the only MRC text available:", "MRC 詳細の取得に失敗したため、一覧の説明のみを使用します:", StringComparison.Ordinal)
            .Replace("Foundry returned an empty response.", "Foundry から空の応答が返されました。", StringComparison.Ordinal)
            .Replace("Azure CLI authentication and model deployment succeeded.", "Azure CLI 認証とモデル デプロイへの接続に成功しました。", StringComparison.Ordinal)
            .Replace("Azure CLI is unavailable:", "Azure CLI を使用できません:", StringComparison.Ordinal)
            .Replace("Run 'az login' for the intended tenant:", "対象テナントで 'az login' を実行してください:", StringComparison.Ordinal)
            .Replace("Authenticated, but RBAC does not permit model inference.", "認証には成功しましたが、RBAC によりモデル推論が許可されていません。", StringComparison.Ordinal)
            .Replace("Connected, but expected roadmap tools were not found.", "接続しましたが、必要なロードマップ ツールが見つかりませんでした。", StringComparison.Ordinal)
            .Replace("Connected.", "接続しました。", StringComparison.Ordinal)
            .Replace("tools discovered.", "個のツールを検出しました。", StringComparison.Ordinal);
    }

    public void SetLanguage(UiLanguage language)
    {
        if (Language == language)
        {
            ApplyCulture();
            return;
        }

        Language = language;
        ApplyCulture();
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyCulture()
    {
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
    }
}
