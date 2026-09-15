using Microsoft.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.Extensions.DependencyInjection;
using ReleaseCommunications.App.Localization;
using System.Runtime.InteropServices;
using Windows.Graphics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ReleaseCommunications.App;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window, IDisposable
{
    private readonly AppLocalization _localization;
    private TaskTrayIcon? _taskTrayIcon;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint windowHandle);

    public MainWindow()
    {
        InitializeComponent();
        _localization = App.Services.GetRequiredService<AppLocalization>();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon(@"Assets\AppIcon.ico");
        var windowHandle = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        var scale = GetDpiForWindow(windowHandle) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(1240 * scale), (int)(800 * scale)));

        UpdateLocalizedChrome();
        _localization.LanguageChanged += OnLanguageChanged;
        Closed += OnClosed;
        RootFrame.Navigate(typeof(MainPage));
    }

    public void InitializeTaskTrayIcon()
    {
        _taskTrayIcon ??= new TaskTrayIcon(this, _localization);
    }

    private void OnLanguageChanged(object? sender, EventArgs e) =>
        UpdateLocalizedChrome();

    private void UpdateLocalizedChrome()
    {
        Title = _localization.Get("AppTitle");
        AppTitleBar.Title = _localization.Get("AppTitle");
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Dispose();
    }

    public void Dispose()
    {
        _localization.LanguageChanged -= OnLanguageChanged;
        _taskTrayIcon?.Dispose();
        GC.SuppressFinalize(this);
    }
}
