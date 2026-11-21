using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Wessl.App.Services;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public sealed class MainWindow(ContainerService containers, ContainerPolling polling, AppSettings settings, ImageService images) : Component
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

        return VStack(12,
            HStack(12,
                TextBlock(AppStrings.Get("ContainersHeading")).FontSize(24),
                Button(AppStrings.Get("ImagesOpenButton"), ShowImages),
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
