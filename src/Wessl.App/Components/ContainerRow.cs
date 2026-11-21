using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Wessl.App.Services;
using Wessl.Domain;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public static class ContainerRow
{
    public static Element Create(
        ContainerInfo container,
        ContainerService service,
        Action<ContainerInfo> onOpenShell,
        bool confirmRemove,
        Action requestRemove,
        Action cancelRemove)
    {
        var ports = container.Ports.Count == 0
            ? AppStrings.Get("NoPorts")
            : string.Join(", ", container.Ports);

        return VStack(2,
            TextBlock(container.Name).SemiBold(),
            TextBlock(AppStrings.Format("ContainerDetails", container.Image, AppStrings.GetContainerState(container.State))),
            TextBlock(AppStrings.Format("PortsLabel", ports)),
            container.State == ContainerState.Running
                ? Button(AppStrings.Get("StopButton"), () => _ = service.StopAsync(container))
                : Button(AppStrings.Get("StartButton"), () => _ = service.StartAsync(container)),
            container.State == ContainerState.Running
                ? Button(AppStrings.Get("RestartButton"), () => _ = service.RestartAsync(container))
                : TextBlock(string.Empty),
            container.State == ContainerState.Running
                ? Button(AppStrings.Get("OpenShellButton"), () => onOpenShell(container))
                : TextBlock(string.Empty),
            confirmRemove
                ? VStack(2,
                    TextBlock(AppStrings.Format("RemoveConfirmation", container.Name)),
                    Button(AppStrings.Get("ConfirmRemoveButton"), () => _ = service.RemoveAsync(container)),
                    Button(AppStrings.Get("CancelButton"), cancelRemove))
                : Button(AppStrings.Get("RemoveButton"), requestRemove));
    }
}
