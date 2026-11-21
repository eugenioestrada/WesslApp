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
        UseEffect(() =>
        {
            var revision = 0;
            void OnChanged() => setRevision(++revision);
            containers.Changed += OnChanged;
            return () => containers.Changed -= OnChanged;
        }, containers);

        return VStack(12,
            TextBlock("Containers").FontSize(24),
            containers.Error is { } error
                ? TextBlock($"Unable to refresh: {error}")
                : ContainerList.Create(containers.Containers, containers.OpenShell),
            containers.ShellError is { } shellError
                ? TextBlock($"Unable to open shell: {shellError}")
                : TextBlock(string.Empty),
            Button("Refresh", () => _ = containers.RefreshAsync())
        ).Padding(20);
    }
}
