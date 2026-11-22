using Microsoft.UI.Reactor;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Wessl.App.Components;
using Wessl.Domain;
using Windows.Foundation;
using Windows.Storage.Pickers;
using Windows.UI.ViewManagement;

namespace Wessl.App.Services;

public sealed partial class AppLifecycle(
    ContainerService containers,
    ContainerPolling polling,
    AppSettings settings,
    IStartupManager startupManager,
    ImageService images,
    IContainerLogsClient logsClient)
{
    private DispatcherQueueTimer? _pollTimer;
    private DispatcherQueueTimer? _statsTimer;
    private ReactorWindow? _window;
    private ReactorWindow? _menuHostWindow;
    private ReactorTrayIcon? _tray;
    private MenuFlyout? _trayMenu;
    private FrameworkElement? _pendingMenuTarget;
    private RoutedEventHandler? _pendingMenuLoaded;
    private Storyboard? _panelAnimation;
    private bool _panelOpening;
    private bool _panelClosing;
    private int _panelOpeningRevision;
    private readonly List<ReactorWindow> _logsWindows = [];

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
        var menu = TrayMenu.Create(
            OpenWindow,
            OpenSettings,
            StopAllFromTray,
            containers.HasRunningContainers,
            Exit);
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
        ShowMainWindow(window);
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
        ShowMainWindow(window);
        UpdatePolling(window);
    }

    private void StopAllFromTray()
    {
        CancelPendingMenu();
        _trayMenu?.Hide();
        _trayMenu = null;
        _menuHostWindow?.Hide();

        var window = EnsureMainWindow();
        ShowMainWindow(window);
        UpdatePolling(window);
        _mainComponent?.RequestStopAll();
    }

    private MainWindow? _mainComponent;

    private void ShowMainWindow(ReactorWindow window)
    {
        _panelOpening = true;
        _panelClosing = false;
        var revision = ++_panelOpeningRevision;
        window.Show();
        window.AppWindow.MoveInZOrderAtTop();
        ActivateMainWindow(window);
        AnimatePanel(window, entering: true);

        // Let the tray/menu finish releasing activation before focusing the panel again.
        if (!window.NativeWindow.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (revision != _panelOpeningRevision || !ReferenceEquals(_window, window))
            {
                return;
            }

            if (window.AppWindow.IsVisible)
            {
                ActivateMainWindow(window);
            }
            _panelOpening = false;
        }))
        {
            throw new InvalidOperationException("The tray panel activation could not be dispatched.");
        }
    }

    private static void ActivateMainWindow(ReactorWindow window)
    {
        window.Activate();
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(window.NativeWindow);
        if (SetForegroundWindow(handle) == 0 && GetForegroundWindow() != handle)
        {
            Trace.TraceWarning("Windows denied foreground activation of the tray panel.");
        }
    }

    [LibraryImport("user32.dll")]
    private static partial int SetForegroundWindow(nint window);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    private void AnimatePanel(ReactorWindow window, bool entering)
    {
        var root = window.NativeWindow.Content as FrameworkElement
            ?? throw new InvalidOperationException("The main window has no framework root element.");
        var previousAnimation = _panelAnimation;
        _panelAnimation = null;
        previousAnimation?.Stop();
        root.Opacity = 1;

        if (!new UISettings().AnimationsEnabled)
        {
            if (!entering)
            {
                window.Hide();
            }
            return;
        }

        if (entering)
        {
            root.Opacity = 0;
        }
        var animation = new DoubleAnimation
        {
            To = entering ? 1 : 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(entering ? 160 : 100)),
            EasingFunction = new CubicEase
            {
                EasingMode = entering ? EasingMode.EaseOut : EasingMode.EaseIn
            }
        };
        Storyboard.SetTarget(animation, root);
        Storyboard.SetTargetProperty(animation, nameof(UIElement.Opacity));
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_panelAnimation, storyboard))
            {
                return;
            }
            if (!entering)
            {
                window.Hide();
            }
            root.Opacity = 1;
            _panelAnimation = null;
            storyboard.Stop();
        };
        _panelAnimation = storyboard;
        storyboard.Begin();
    }

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
                new WindowSpec
                {
                    Title = "Wessl",
                    Width = 440,
                    Height = 460,
                    IsMinimizable = false,
                    IsMaximizable = false
                },
                () =>
                {
                    _mainComponent = new MainWindow(containers, polling, settings, images, OpenLogs);
                    return _mainComponent;
                });
            if (_window.AppWindow.Presenter is not OverlappedPresenter presenter)
            {
                throw new InvalidOperationException("The main window requires an overlapped presenter.");
            }
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(true, false);
            _window.NativeWindow.SystemBackdrop = new MicaBackdrop();
            ApplyTheme(_window);
            var window = _window;
            window.NativeWindow.Activated += (_, args) =>
            {
                if (args.WindowActivationState == WindowActivationState.Deactivated
                    && !_panelOpening && !_panelClosing && window.AppWindow.IsVisible)
                {
                    _panelClosing = true;
                    AnimatePanel(window, entering: false);
                }
                else if (args.WindowActivationState != WindowActivationState.Deactivated && _panelClosing)
                {
                    ShowMainWindow(window);
                }
            };
            _pollTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _statsTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
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
            _statsTimer.Tick += (_, _) =>
            {
                if (IsVisible(window))
                {
                    polling.TickStats();
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
                var animation = _panelAnimation;
                _panelAnimation = null;
                animation?.Stop();
                _panelOpening = false;
                _panelClosing = false;
                _pollTimer?.Stop();
                _pollTimer = null;
                _statsTimer?.Stop();
                _statsTimer = null;
                polling.Stop();
                _window = null;
                _mainComponent = null;
            };
        }

        return _window;
    }

    private void OpenLogs(ContainerInfo container)
    {
        var session = new ContainerLogsSession(logsClient, action =>
        {
            var dispatcher = ReactorApp.UIDispatcher
                ?? throw new InvalidOperationException("The UI dispatcher is not available.");
            if (!dispatcher.TryEnqueue(() => action()))
            {
                throw new InvalidOperationException("Could not dispatch the log update.");
            }
        });
        ReactorWindow? window = null;
        window = ReactorApp.OpenWindow(
            new WindowSpec { Title = AppStrings.Format("LogsTitle", container.Name), Width = 720, Height = 480 },
            () => new LogsWindow(
                session,
                () => PickLogsSavePathAsync(window!, container.Name)));
        _logsWindows.Add(window);
        window.Closed += (_, _) =>
        {
            session.Stop();
            _logsWindows.Remove(window);
        };
        window.Show();
        window.Activate();
        session.Start(containers.Containers.FirstOrDefault(item => item.Id == container.Id)
            ?? container with { State = ContainerState.Unknown });
    }

    private static async Task<string?> PickLogsSavePathAsync(ReactorWindow owner, string containerName)
    {
        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add(AppStrings.Get("LogsTextFileType"), [".txt"]);
        picker.SuggestedFileName = $"{containerName}-logs";
        WinRT.Interop.InitializeWithWindow.Initialize(
            picker,
            WinRT.Interop.WindowNative.GetWindowHandle(owner.NativeWindow));
        return (await picker.PickSaveFileAsync())?.Path;
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
        if (_statsTimer is { } statsTimer)
        {
            statsTimer.Interval = polling.StatsRefreshInterval;
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
            if (_statsTimer is { IsRunning: false } statsTimer)
            {
                polling.TickStats();
                statsTimer.Start();
            }
        }
        else
        {
            _pollTimer?.Stop();
            _statsTimer?.Stop();
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
        _statsTimer?.Stop();
        polling.Stop();
        containers.Changed -= UpdateTrayIcon;
        settings.Changed -= OnSettingsChanged;
        _trayMenu?.Hide();
        _trayMenu = null;
        _tray?.Close();
        _tray = null;
        _menuHostWindow?.Close();
        _menuHostWindow = null;
        foreach (var window in _logsWindows.ToArray())
        {
            window.Close();
        }
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
