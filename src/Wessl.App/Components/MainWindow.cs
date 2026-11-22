using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml.Controls;
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
    ContainerDetailsService details,
    Action<Wessl.Domain.ContainerInfo> openLogs) : Component
{
    private Action<int>? _setRevision;
    private int _revision;
    private bool _showSettings;
    private bool _showImages;
    private bool _showStopAllConfirmation;
    private bool _showStopAllResult;
    private bool _showDetailsDialog;
    private ContainerInfo? _detailsContainer;
    private ContainerInfo[] _stopAllContainers = [];
    private string _stopAllResult = string.Empty;

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
            details.Changed += OnChanged;
            return () =>
            {
                containers.Changed -= OnChanged;
                settings.Changed -= OnChanged;
                images.Changed -= OnChanged;
                details.Changed -= OnChanged;
            };
        }, containers, settings, images, details);

        if (_showSettings)
        {
            return WithDialogs(SettingsPage.Create(settings, ShowContainers));
        }

        if (_showImages)
        {
            return WithDialogs(ImagePage.Create(images, confirmPrune, () => setConfirmPrune(true),
                () => setConfirmPrune(false), ShowContainers));
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
            messages.Add(InfoBar(message: AppStrings.Format(shellError)).Error()
                with { IsOpen = true, IsClosable = false });
        }

        return WithDialogs(Grid(
            [GridSize.Star()],
            [GridSize.Auto, GridSize.Auto, GridSize.Auto, GridSize.Star()],
            Grid([GridSize.Star(), GridSize.Auto, GridSize.Auto, GridSize.Auto], [GridSize.Auto],
                    TextBlock("Wessl").FontSize(16).SemiBold()
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Grid(column: 0),
                    Button(Icon(FontIcon("\uE8B9", fontSize: 16)), ShowImages)
                        .SubtleButton()
                        .ToolTip(imagesLabel)
                        .AutomationName(imagesLabel)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Grid(column: 1),
                    Button(Icon(FontIcon("\uE713", fontSize: 16)), ShowSettings)
                        .SubtleButton()
                        .ToolTip(settingsLabel)
                        .AutomationName(settingsLabel)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Grid(column: 2),
                    Button(Icon(FontIcon("\uE72C", fontSize: 16)), () => _ = polling.RefreshAsync())
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
                OpenDetails,
                pendingRemoval,
                id => setPendingRemoval(id),
                () => setPendingRemoval(null),
                settings.LanguageTag).Margin(16, 0, 16, 16).Grid(row: 3)
        ).Padding(0, 8, 0, 0));
    }

    private Element WithDialogs(Element content) =>
        Grid([GridSize.Star()], [GridSize.Star()],
            content,
            (ContentDialog(
                AppStrings.Get("TrayStopAllConfirmationTitle"),
                TextBlock(AppStrings.Format("TrayStopAllConfirmation", _stopAllContainers.Length)),
                AppStrings.Get("TrayStopAllConfirm")) with
                {
                    IsOpen = _showStopAllConfirmation,
                    CloseButtonText = AppStrings.Get("CancelButton"),
                    DefaultButton = ContentDialogButton.Close
                })
                .Closed(OnStopAllConfirmationClosed),
            (ContentDialog(
                AppStrings.Get("TrayStopAllResultTitle"),
                TextBlock(_stopAllResult).TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap),
                AppStrings.Get("CloseButton")) with
                {
                    IsOpen = _showStopAllResult,
                    DefaultButton = ContentDialogButton.Close
                })
                .Closed(_ =>
                {
                    _showStopAllResult = false;
                    RequestRender();
                }),
            _detailsContainer is { } detailsContainer
                ? (ContentDialog(
                    AppStrings.Format("ContainerDetailsTitle", detailsContainer.Name),
                    ContainerDetailsView.Create(detailsContainer, details),
                    AppStrings.Get("CloseButton")) with
                {
                    IsOpen = _showDetailsDialog,
                    DefaultButton = ContentDialogButton.Primary
                })
                .Closed(_ => CloseDetails())
                : TextBlock(string.Empty)
        );

    public void ShowSettings()
    {
        _showImages = false;
        Navigate(true);
    }

    public void RequestStopAll()
    {
        _stopAllContainers = containers.Containers
            .Where(container => container.State == Wessl.Domain.ContainerState.Running)
            .ToArray();
        if (_stopAllContainers.Length == 0)
        {
            return;
        }

        _showSettings = false;
        _showImages = false;
        _showStopAllConfirmation = true;
        RequestRender();
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

    private void OpenDetails(ContainerInfo container)
    {
        _detailsContainer = container;
        _showDetailsDialog = true;
        RequestRender();
        _ = details.InspectAsync(container);
    }

    private void CloseDetails()
    {
        _detailsContainer = null;
        _showDetailsDialog = false;
        details.Clear();
        RequestRender();
    }

    private void OnStopAllConfirmationClosed(ContentDialogResult result)
    {
        _showStopAllConfirmation = false;
        RequestRender();
        if (result == ContentDialogResult.Primary)
        {
            _ = StopAllAsync();
        }
    }

    private async Task StopAllAsync()
    {
        var result = await containers.StopAllAsync(_stopAllContainers);
        _stopAllResult = AppStrings.Format(
            "TrayStopAllResult",
            result.Status is { } status ? AppStrings.Format(status) : string.Empty,
            result.Error is { } error ? AppStrings.Format(error) : string.Empty);
        _showStopAllResult = true;
        RequestRender();
    }

    private void Navigate(bool showSettings)
    {
        _showSettings = showSettings;
        RequestRender();
    }

    private void RequestRender() => _setRevision?.Invoke(++_revision);
}
