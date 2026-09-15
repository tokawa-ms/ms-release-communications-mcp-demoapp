using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using ReleaseCommunications.App.Localization;

namespace ReleaseCommunications.App;

internal sealed class TaskTrayIcon : IDisposable
{
    private readonly MainWindow _window;
    private readonly AppLocalization _localization;
    private readonly Microsoft.UI.Xaml.Controls.MenuFlyout _menu = new();
    private readonly MenuFlyoutItem _showItem = new();
    private readonly MenuFlyoutItem _exitItem = new();
    private readonly TaskbarIcon _icon;

    public TaskTrayIcon(MainWindow window, AppLocalization localization)
    {
        _window = window;
        _localization = localization;

        _showItem.Command = new RelayCommand(ShowWindow);
        _exitItem.Command = new RelayCommand(ExitApplication);
        _menu.Items.Add(_showItem);
        _menu.Items.Add(new MenuFlyoutSeparator());
        _menu.Items.Add(_exitItem);

        _icon = new TaskbarIcon
        {
            IconSource = new BitmapImage(
                new Uri("ms-appx:///Assets/TaskTrayIcon.ico")),
            ContextFlyout = _menu,
            LeftClickCommand = new RelayCommand(ShowWindow),
            NoLeftClickDelay = true
        };
        UpdateLocalizedText();
        _localization.LanguageChanged += OnLanguageChanged;
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    private void OnLanguageChanged(object? sender, EventArgs e) =>
        UpdateLocalizedText();

    private void UpdateLocalizedText()
    {
        _icon.ToolTipText = _localization.Get("AppTitle");
        _showItem.Text = _localization.Get("TrayShow");
        _exitItem.Text = _localization.Get("TrayExit");
    }

    private void ShowWindow()
    {
        _window.AppWindow.Show();
        _window.Activate();
    }

    private void ExitApplication()
    {
        Dispose();
        _window.Close();
    }

    public void Dispose()
    {
        _localization.LanguageChanged -= OnLanguageChanged;
        _icon.Dispose();
    }
}
