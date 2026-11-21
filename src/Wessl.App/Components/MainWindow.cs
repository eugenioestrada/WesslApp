using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Wessl.App.Services;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public sealed class MainWindow(ContainerService containers) : Component
{
    public override Element Render()
    {
        var (_, setRevision) = UseState(0);
        var (pendingRemoval, setPendingRemoval) = UseState<string?>(null);
        UseEffect(() =>
        {
            var revision = 0;
            void OnChanged() => setRevision(++revision);
            containers.Changed += OnChanged;
            return () => containers.Changed -= OnChanged;
        }, containers);

        return VStack(12,
            TextBlock("Containers").FontSize(24),
            containers.Error is { } error ? TextBlock(error) : TextBlock(string.Empty),
            containers.Status is { } status ? TextBlock(status) : TextBlock(string.Empty),
            ContainerList.Create(
                containers.Containers,
                containers,
                pendingRemoval,
                id => setPendingRemoval(id),
                () => setPendingRemoval(null)),
            Button("Refresh", () => _ = containers.RefreshAsync())
        ).Padding(20);
    }
}
