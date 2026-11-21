using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Wessl.Domain;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public static class ContainerRow
{
    public static Element Create(ContainerInfo container, Action<ContainerInfo> onOpenShell)
    {
        var ports = container.Ports.Count == 0
            ? "None"
            : string.Join(", ", container.Ports);

        return VStack(2,
            TextBlock(container.Name).SemiBold(),
            TextBlock($"{container.Image} · {container.State}"),
            TextBlock($"Ports: {ports}"),
            container.State == ContainerState.Running
                ? Button("Open shell", () => onOpenShell(container))
                : TextBlock(string.Empty));
    }
}
