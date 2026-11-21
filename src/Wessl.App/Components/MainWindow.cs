using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Wessl.App.Services;
using Wessl.Domain;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public sealed class MainWindow(
    ContainerService containers,
    ContainerPolling polling,
    AppSettings settings,
    ImageService images,
    Action<Wessl.Domain.ContainerInfo> openLogs) : Component
{
    private Action<int>? _setRevision;
    private int _revision;
    private bool _showSettings;
    private bool _showImages;

    public override Element Render()
    {
        var (_, setRevision) = UseState(0);
        _setRevision = setRevision;
        var (pendingRemoval, setPendingRemoval) = UseState<string?>(null);
        var (confirmPrune, setConfirmPrune) = UseState(false);
        UseEffect(() =>
        {
            void OnChanged() => RequestRender();
            containers.Changed += OnChanged;
            settings.Changed += OnChanged;
            images.Changed += OnChanged;
            return () =>
            {
                containers.Changed -= OnChanged;
                settings.Changed -= OnChanged;
                images.Changed -= OnChanged;
            };
        }, containers, settings, images);

        if (_showSettings)
        {
            return SettingsPage.Create(settings, ShowContainers);
        }

        if (_showImages)
        {
            return ImagePage.Create(images, confirmPrune, () => setConfirmPrune(true),
                () => setConfirmPrune(false), ShowContainers);
        }

        var runningCount = containers.Containers.Count(container => container.State == ContainerState.Running);
        var refreshLabel = AppStrings.Get("RefreshButton");
        var imagesLabel = AppStrings.Get("ImagesOpenButton");
        var settingsLabel = AppStrings.Get("SettingsOpenButton");
        var messages = new List<Element>();
        if (containers.Error is { } error)
        {
            messages.Add(InfoBar(message: AppStrings.Format(error)).Error() with { IsOpen = true, IsClosable = false });
        }
        if (containers.Status is { } status)
        {
            var message = InfoBar(message: AppStrings.Format(status));
            messages.Add((status.ResourceKey.EndsWith("Pending", StringComparison.Ordinal) ? message.Informational() : message.Success())
                with { IsOpen = true, IsClosable = false });
        }
        if (containers.ShellError is { } shellError)
        {
            messages.Add(InfoBar(message: AppStrings.Format("OpenShellError", shellError)).Error()
                with { IsOpen = true, IsClosable = false });
        }

        return Grid(
            [GridSize.Star()],
            [GridSize.Auto, GridSize.Auto, GridSize.Auto, GridSize.Star()],
            Grid([GridSize.Star(), GridSize.Auto, GridSize.Auto, GridSize.Auto], [GridSize.Auto],
                    TextBlock("Wessl").FontSize(16).SemiBold()
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Grid(column: 0),
                    Button(Icon(FontIcon("\uE8B9", "Segoe Fluent Icons, Segoe MDL2 Assets", 14)), ShowImages)
                        .SubtleButton()
                        .ToolTip(imagesLabel)
                        .AutomationName(imagesLabel)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Grid(column: 1),
                    Button(Icon(FontIcon("\uE713", "Segoe Fluent Icons, Segoe MDL2 Assets", 14)), ShowSettings)
                        .SubtleButton()
                        .ToolTip(settingsLabel)
                        .AutomationName(settingsLabel)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Grid(column: 2),
                    Button(Icon(FontIcon("\uE72C", "Segoe Fluent Icons, Segoe MDL2 Assets", 14)), () => _ = polling.RefreshAsync())
                        .SubtleButton()
                        .ToolTip(refreshLabel)
                        .AutomationName(refreshLabel)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Grid(column: 3))
                .Margin(16, 4, 16, 0)
                .Grid(row: 0),
            TextBlock(AppStrings.Format("ContainerSummary", containers.Containers.Count, runningCount))
                .TextWrapping()
                .Margin(16, 8, 16, 8)
                .Grid(row: 1),
            VStack(4, messages.ToArray()).Margin(16, 0).Grid(row: 2),
            ContainerList.Create(
                containers.Containers,
                containers,
                containers.OpenShell,
                openLogs,
                pendingRemoval,
                id => setPendingRemoval(id),
                () => setPendingRemoval(null)).Margin(16, 0, 16, 16).Grid(row: 3)
        );
    }

    public void ShowSettings()
    {
        _showImages = false;
        Navigate(true);
    }

    private void ShowImages()
    {
        _showSettings = false;
        _showImages = true;
        RequestRender();
        _ = images.RefreshAsync();
    }

    private void ShowContainers()
    {
        _showImages = false;
        Navigate(false);
    }

    private void Navigate(bool showSettings)
    {
        _showSettings = showSettings;
        RequestRender();
    }

    private void RequestRender() => _setRevision?.Invoke(++_revision);
}
