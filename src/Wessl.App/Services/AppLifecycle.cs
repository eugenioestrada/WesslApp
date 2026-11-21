using Microsoft.UI.Reactor;
using Wessl.App.Components;

namespace Wessl.App.Services;

public sealed class AppLifecycle(ContainerService containers)
{
    private ReactorWindow? _window;
    private ReactorTrayIcon? _tray;

    public void Start()
    {
        ReactorApp.ShutdownPolicy = ShutdownPolicy.Explicit;
        var icon = WindowIcon.FromPath(Path.Combine(AppContext.BaseDirectory, "Assets", "Wessl.ico"));
        _tray = ReactorApp.OpenTrayIcon(new TrayIconSpec(icon, "Wessl"));
        _tray.Click += (_, _) => ToggleWindow();
        _tray.RightClick += (_, _) => _tray.ShowFlyout(TrayMenu.Create(
            OpenWindow,
            () =>
            {
                _tray.HideFlyout();
                _ = containers.RefreshAsync();
            },
            Exit));
        _ = containers.RefreshAsync();
    }

    private void ToggleWindow()
    {
        if (_window is { IsVisible: true })
        {
            _window.Hide();
        }
        else
        {
            OpenWindow();
        }
    }

    private void OpenWindow()
    {
        _tray?.HideFlyout();
        if (_window is null)
        {
            _window = ReactorApp.OpenWindow(
                new WindowSpec { Title = "Wessl", Width = 420, Height = 360 },
                () => new MainWindow(containers));
            _window.Closed += (_, _) => _window = null;
        }

        _window.Show();
        _window.Activate();
    }

    private void Exit()
    {
        _tray?.Close();
        ReactorApp.Exit();
    }
}
