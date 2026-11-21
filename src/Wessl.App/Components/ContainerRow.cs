using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Wessl.Domain;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public static class ContainerRow
{
    public static Element Create(ContainerInfo container) =>
        VStack(2,
            TextBlock(container.Name).SemiBold(),
            TextBlock($"{container.Image} · {container.State}"));
}
