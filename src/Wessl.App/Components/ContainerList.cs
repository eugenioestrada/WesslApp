using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Wessl.App.Services;
using Wessl.Domain;
using Windows.ApplicationModel.DataTransfer;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public static class ContainerList
{
    public static Element Create(
        IReadOnlyList<ContainerInfo> containers,
        ContainerService service,
        Action<ContainerInfo> onOpenShell,
        Action<ContainerInfo> onOpenLogs,
        string? pendingRemoval,
        Action<string> requestRemove,
        Action cancelRemove) =>
        service.IsWslcUnavailable
            ? Component<UnavailableWslc>()
            : containers.Count == 0
            ? TextBlock(AppStrings.Get("EmptyContainers"))
            : ScrollViewer(VStack(
                8,
                containers.Select(container =>
                    ContainerRow.Create(
                        container,
                        service,
                        onOpenShell,
                        onOpenLogs,
                        pendingRemoval == container.Id,
                        () => requestRemove(container.Id),
                        cancelRemove).WithKey(container.Id)).ToArray()))
                .HorizontalScrollMode(Microsoft.UI.Xaml.Controls.ScrollMode.Disabled);
}

public sealed class UnavailableWslc : Component
{
    public override Element Render()
    {
        var (status, setStatus) = UseState(string.Empty);
        return VStack(8,
            TextBlock(AppStrings.Get("WslcUnavailable")).TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap),
            Button(AppStrings.Get("CopyUpdateCommand"), () =>
            {
                try
                {
                    var package = new DataPackage();
                    package.SetText("wsl --update");
                    Clipboard.SetContent(package);
                    setStatus(AppStrings.Get("UpdateCommandCopied"));
                }
                catch (Exception exception)
                {
                    setStatus(AppStrings.Format("UpdateCommandCopyError", exception.Message));
                }
            }),
            TextBlock(status).TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap));
    }
}
