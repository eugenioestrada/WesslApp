using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml.Controls;
using Wessl.App.Services;
using Wessl.Domain;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public sealed class MainWindow(ContainerService containers, ContainerPolling polling, AppSettings settings) : Component
{
    private Action<int>? _setRevision;
    private int _revision;
    private bool _showSettings;
    private bool _showStopAllConfirmation;
    private bool _showStopAllResult;
    private ContainerInfo[] _stopAllContainers = [];
    private string _stopAllResult = string.Empty;

    public override Element Render()
    {
        var (_, setRevision) = UseState(0);
        _setRevision = setRevision;
        var (pendingRemoval, setPendingRemoval) = UseState<string?>(null);
        UseEffect(() =>
        {
            void OnChanged() => RequestRender();
            containers.Changed += OnChanged;
            settings.Changed += OnChanged;
            return () =>
            {
                containers.Changed -= OnChanged;
                settings.Changed -= OnChanged;
            };
        }, containers, settings);

        if (_showSettings)
        {
            return SettingsPage.Create(settings, ShowContainers);
        }

        return VStack(12,
            HStack(12,
                TextBlock(AppStrings.Get("ContainersHeading")).FontSize(24),
                Button(AppStrings.Get("SettingsOpenButton"), ShowSettings)),
            containers.Error is { } error
                ? TextBlock(AppStrings.Format(error))
                : TextBlock(string.Empty),
            containers.Status is { } status
                ? TextBlock(AppStrings.Format(status))
                : TextBlock(string.Empty),
            ContainerList.Create(
                containers.Containers,
                containers,
                containers.OpenShell,
                pendingRemoval,
                id => setPendingRemoval(id),
                () => setPendingRemoval(null)),
            containers.ShellError is { } shellError
                ? TextBlock(AppStrings.Format("OpenShellError", shellError))
                : TextBlock(string.Empty),
            Button(AppStrings.Get("RefreshButton"), () => _ = polling.RefreshAsync()),
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
                })
        ).Padding(20);
    }

    public void ShowSettings() => Navigate(true);

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
        _showStopAllConfirmation = true;
        RequestRender();
    }

    private void ShowContainers() => Navigate(false);

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
