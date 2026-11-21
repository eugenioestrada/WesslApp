using Microsoft.UI.Reactor.Core;
using Wessl.App.Services;
using Wessl.Domain;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public static class ContainerList
{
    public static Element Create(IReadOnlyList<ContainerInfo> containers) =>
        containers.Count == 0
            ? TextBlock(AppStrings.Get("EmptyContainers"))
            : ScrollViewer(VStack(8, containers.Select(ContainerRow.Create).ToArray()));
}
