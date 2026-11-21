using Microsoft.UI.Reactor;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Wessl.App.Components;
using Windows.Foundation;

namespace Wessl.App.Services;

public sealed class AppLifecycle(ContainerService containers, ContainerPolling polling)
{
    private DispatcherQueueTimer? _pollTimer;
    private ReactorWindow? _window;
    private ReactorWindow? _menuHostWindow;
    private ReactorTrayIcon? _tray;
    private MenuFlyout? _trayMenu;

    public void Start()
    {
        ReactorApp.ShutdownPolicy = ShutdownPolicy.Explicit;
        _tray = ReactorApp.OpenTrayIcon(CreateTrayIconSpec());
        _tray.Click += (_, _) => OpenWindow();
        _tray.RightClick += (_, _) => ShowTrayMenu();
        containers.Changed += UpdateTrayIcon;
    }

    private void ShowTrayMenu()
    {
        _trayMenu?.Hide();
        _trayMenu = null;

        var cursorPosition = TrayPlacement.GetCursorPosition();
        var host = EnsureMenuHostWindow();
        TrayPlacement.MoveMenuHost(host.AppWindow, cursorPosition);
        host.Show();

        var target = host.NativeWindow.Content
            ?? throw new InvalidOperationException("The tray menu host has no content element.");
        var menu = TrayMenu.Create(OpenWindow, Exit);
        _trayMenu = menu;
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_trayMenu, menu))
            {
                _trayMenu = null;
            }

            host.Hide();
        };
        menu.ShowAt(target, new Point(0, 0));
    }

    private void OpenWindow()
    {
        _trayMenu?.Hide();
        _trayMenu = null;
        _menuHostWindow?.Hide();

        var cursorPosition = TrayPlacement.GetCursorPosition();
        var window = EnsureMainWindow();
        TrayPlacement.MoveWindowNearTray(window.AppWindow, cursorPosition);
        window.Show();
        window.Activate();
        UpdatePolling(window);
    }

    private TrayIconSpec CreateTrayIconSpec()
    {
        var running = containers.HasRunningContainers;
        var status = !containers.HasRefreshed ? "Unknown" : running ? "Running" : "Idle";
        return new TrayIconSpec(TrayStatusIcon.Create(running), $"Wessl - {status}");
    }

    private void UpdateTrayIcon()
    {
        _tray?.Update(CreateTrayIconSpec());
    }

    private ReactorWindow EnsureMainWindow()
    {
        if (_window is null)
        {
            _window = ReactorApp.OpenWindow(
                new WindowSpec { Title = "Wessl", Width = 420, Height = 360 },
                () => new MainWindow(containers, polling));
            var window = _window;
            _pollTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _pollTimer.Interval = TimeSpan.FromSeconds(10);
            _pollTimer.Tick += (_, _) =>
            {
                if (IsVisible(window))
                {
                    polling.Tick();
                }
                else
                {
                    UpdatePolling(window);
                }
            };
            window.NativeWindow.VisibilityChanged += (_, _) => UpdatePolling(window);
            window.AppWindow.Changed += (_, _) => UpdatePolling(window);
            window.Closed += (_, _) =>
            {
                _pollTimer?.Stop();
                _pollTimer = null;
                polling.Stop();
                _window = null;
            };
        }

        return _window;
    }

    private static bool IsVisible(ReactorWindow window) =>
        window.AppWindow.IsVisible &&
        (window.AppWindow.Presenter is not OverlappedPresenter presenter ||
         presenter.State != OverlappedPresenterState.Minimized);

    private void UpdatePolling(ReactorWindow window)
    {
        if (!ReferenceEquals(_window, window))
        {
            return;
        }

        if (IsVisible(window))
        {
            polling.Start();
            if (_pollTimer is { IsRunning: false } timer)
            {
                timer.Start();
            }
        }
        else
        {
            _pollTimer?.Stop();
            polling.Stop();
        }
    }

    private ReactorWindow EnsureMenuHostWindow()
    {
        if (_menuHostWindow is null)
        {
            _menuHostWindow = ReactorApp.OpenWindow(
                new WindowSpec
                {
                    Title = string.Empty,
                    Width = 1,
                    Height = 1,
                    Style = WindowStyle.None,
                    ShowInTaskbar = false,
                    ShowInSwitcher = false
                },
                () => new TrayMenuHost());
            _menuHostWindow.Closed += (_, _) => _menuHostWindow = null;
        }

        return _menuHostWindow;
    }

    private void Exit()
    {
        _pollTimer?.Stop();
        polling.Stop();
        containers.Changed -= UpdateTrayIcon;
        _trayMenu?.Hide();
        _trayMenu = null;
        _tray?.Close();
        _tray = null;
        _menuHostWindow?.Close();
        _menuHostWindow = null;
        _window?.Close();
        _window = null;
        ReactorApp.Exit();
    }
}
