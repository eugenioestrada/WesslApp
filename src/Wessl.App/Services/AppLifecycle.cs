using Microsoft.UI.Reactor;
using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Wessl.App.Components;
using Wessl.Domain;
using Windows.Foundation;

namespace Wessl.App.Services;

public sealed class AppLifecycle(
    ContainerService containers,
    ContainerPolling polling,
    AppSettings settings,
    IStartupManager startupManager)
{
    private DispatcherQueueTimer? _pollTimer;
    private ReactorWindow? _window;
    private ReactorWindow? _menuHostWindow;
    private ReactorTrayIcon? _tray;
    private MenuFlyout? _trayMenu;
    private FrameworkElement? _pendingMenuTarget;
    private RoutedEventHandler? _pendingMenuLoaded;

    public void Start()
    {
        AppStrings.SetLanguage(settings.LanguageTag);
        settings.Changed += OnSettingsChanged;
        startupManager.SetEnabled(settings.StartWithWindows);
        ReactorApp.ShutdownPolicy = ShutdownPolicy.Explicit;
        _tray = ReactorApp.OpenTrayIcon(CreateTrayIconSpec());
        _tray.Click += (_, _) => OpenWindow();
        _tray.RightClick += (_, _) => ShowTrayMenu();
        containers.Changed += UpdateTrayIcon;
    }

    private void ShowTrayMenu()
    {
        CancelPendingMenu();
        _trayMenu?.Hide();
        _trayMenu = null;

        var cursorPosition = TrayPlacement.GetCursorPosition();
        var host = EnsureMenuHostWindow();
        host.Activate();
        TrayPlacement.MoveMenuHost(host.AppWindow, cursorPosition);

        var target = host.NativeWindow.Content as FrameworkElement
            ?? throw new InvalidOperationException("The tray menu host has no content element.");
        // The Reactor root can retain its initial large layout despite the tiny native window.
        target.Width = 1;
        target.Height = 1;
        target.HorizontalAlignment = HorizontalAlignment.Left;
        target.VerticalAlignment = VerticalAlignment.Top;
        var menu = TrayMenu.Create(OpenWindow, OpenSettings, Exit);
        _trayMenu = menu;
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_trayMenu, menu))
            {
                _trayMenu = null;
                host.Hide();
            }
        };
        void ShowMenu()
        {
            if (ReferenceEquals(_trayMenu, menu))
            {
                CancelPendingMenu();
                target.UpdateLayout();
                menu.ShowAt(target, new Point(0, 0));
            }
        }

        if (target.IsLoaded)
        {
            if (!target.DispatcherQueue.TryEnqueue(ShowMenu))
            {
                throw new InvalidOperationException("The tray menu could not be dispatched.");
            }
        }
        else
        {
            _pendingMenuTarget = target;
            _pendingMenuLoaded = (_, _) => ShowMenu();
            target.Loaded += _pendingMenuLoaded;
        }
    }

    private void OpenWindow()
    {
        CancelPendingMenu();
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

    private void OpenSettings()
    {
        CancelPendingMenu();
        _trayMenu?.Hide();
        _trayMenu = null;
        _menuHostWindow?.Hide();

        var cursorPosition = TrayPlacement.GetCursorPosition();
        var window = EnsureMainWindow();
        _mainComponent?.ShowSettings();
        TrayPlacement.MoveWindowNearTray(window.AppWindow, cursorPosition);
        window.Show();
        window.Activate();
        UpdatePolling(window);
    }

    private MainWindow? _mainComponent;

    private void OnSettingsChanged()
    {
        AppStrings.SetLanguage(settings.LanguageTag);
        startupManager.SetEnabled(settings.StartWithWindows);
        if (_window is { } window)
        {
            ApplyTheme(window);
            ApplyRefreshInterval();
        }
    }

    private void ApplyTheme(ReactorWindow window)
    {
        var root = window.NativeWindow.Content as FrameworkElement
            ?? throw new InvalidOperationException("The main window has no framework root element.");
        root.RequestedTheme = settings.Theme switch
        {
            AppTheme.Automatic => ElementTheme.Default,
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => throw new InvalidOperationException("The selected theme is not supported.")
        };
    }

    private TrayIconSpec CreateTrayIconSpec()
    {
        var running = containers.HasRunningContainers;
        var status = !containers.HasRefreshed
            ? AppStrings.Get("TrayStatusUnknown")
            : running
                ? AppStrings.Get("TrayStatusRunning")
                : AppStrings.Get("TrayStatusIdle");
        return new TrayIconSpec(TrayStatusIcon.Create(running), AppStrings.Format("TrayTooltip", status));
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
                () =>
                {
                    _mainComponent = new MainWindow(containers, polling, settings);
                    return _mainComponent;
                });
            ApplyTheme(_window);
            var window = _window;
            _pollTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            ApplyRefreshInterval();
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
                _mainComponent = null;
            };
        }

        return _window;
    }

    private static bool IsVisible(ReactorWindow window) =>
        window.AppWindow.IsVisible &&
        (window.AppWindow.Presenter is not OverlappedPresenter presenter ||
         presenter.State != OverlappedPresenterState.Minimized);

    private void ApplyRefreshInterval()
    {
        if (_pollTimer is { } timer)
        {
            timer.Interval = polling.ActiveRefreshInterval;
        }
    }

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
        CancelPendingMenu();
        _pollTimer?.Stop();
        polling.Stop();
        containers.Changed -= UpdateTrayIcon;
        settings.Changed -= OnSettingsChanged;
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

    private void CancelPendingMenu()
    {
        if (_pendingMenuTarget is not null && _pendingMenuLoaded is not null)
        {
            _pendingMenuTarget.Loaded -= _pendingMenuLoaded;
            _pendingMenuTarget = null;
            _pendingMenuLoaded = null;
        }
    }
}
