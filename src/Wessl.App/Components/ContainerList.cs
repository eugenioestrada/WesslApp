using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Wessl.Domain;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public static class ContainerList
{
    public static Element Create(
        IReadOnlyList<ContainerInfo> containers,
        Action<ContainerInfo> onOpenShell) =>
        containers.Count == 0
            ? TextBlock("No containers found.")
            : ScrollViewer(VStack(
                8,
                containers.Select(container =>
                    ContainerRow.Create(container, onOpenShell).WithKey(container.Id)).ToArray()));
}
