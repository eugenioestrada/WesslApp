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
        bool confirmRemove,
        Action requestRemove,
        Action cancelRemove) =>
        VStack(2,
            TextBlock(container.Name).SemiBold(),
            TextBlock($"{container.Image} · {container.State}"),
            container.State == ContainerState.Running
                ? Button("Stop", () => _ = service.StopAsync(container))
                : Button("Start", () => _ = service.StartAsync(container)),
            container.State == ContainerState.Running
                ? Button("Restart", () => _ = service.RestartAsync(container))
                : TextBlock(string.Empty),
            confirmRemove
                ? VStack(2,
                    TextBlock($"Remove {container.Name}? This permanently deletes the container and stops it if running."),
                    Button("Confirm remove", () => _ = service.RemoveAsync(container)),
                    Button("Cancel", cancelRemove))
                : Button("Remove", requestRemove)
        );
}
