using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Wessl.App.Services;
using Wessl.Domain;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public static class ContainerRow
{
    public static Element Create(ContainerInfo container)
    {
        var ports = container.Ports.Count == 0
            ? AppStrings.Get("NoPorts")
            : string.Join(", ", container.Ports);

        return VStack(2,
            TextBlock(container.Name).SemiBold(),
            TextBlock(AppStrings.Format("ContainerDetails", container.Image, AppStrings.GetContainerState(container.State))),
            TextBlock(AppStrings.Format("PortsLabel", ports)));
    }
}
