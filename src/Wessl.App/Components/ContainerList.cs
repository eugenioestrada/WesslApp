using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Wessl.App.Services;
using Wessl.Domain;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public static class ContainerList
{
    public static Element Create(
        IReadOnlyList<ContainerInfo> containers,
        ContainerService service,
        Action<ContainerInfo> onOpenShell,
        string? pendingRemoval,
        Action<string> requestRemove,
        Action cancelRemove) =>
        containers.Count == 0
            ? TextBlock(AppStrings.Get("EmptyContainers"))
            : ScrollViewer(VStack(
                8,
                containers.Select(container =>
                    ContainerRow.Create(
                        container,
                        service,
                        onOpenShell,
                        pendingRemoval == container.Id,
                        () => requestRemove(container.Id),
                        cancelRemove).WithKey(container.Id)).ToArray()))
                .HorizontalScrollMode(Microsoft.UI.Xaml.Controls.ScrollMode.Disabled);
}
