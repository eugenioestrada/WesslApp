using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Wessl.App.Services;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public sealed class MainWindow(ContainerService containers, ContainerPolling polling, AppSettings settings) : Component
{
    private Action<int>? _setRevision;
    private int _revision;
    private bool _showSettings;

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
            Button(AppStrings.Get("RefreshButton"), () => _ = polling.RefreshAsync())
        ).Padding(20);
    }

    public void ShowSettings() => Navigate(true);

    private void ShowContainers() => Navigate(false);

    private void Navigate(bool showSettings)
    {
        _showSettings = showSettings;
        RequestRender();
    }

    private void RequestRender() => _setRevision?.Invoke(++_revision);
}
