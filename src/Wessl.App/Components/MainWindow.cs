using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Wessl.App.Services;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public sealed class MainWindow(ContainerService containers, ContainerPolling polling) : Component
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
            TextBlock(AppStrings.Get("ContainersHeading")).FontSize(24),
            containers.Error is { } error
                ? TextBlock(AppStrings.Format(error))
                : TextBlock(string.Empty),
            containers.Status is { } status
                ? TextBlock(AppStrings.Format(status))
                : TextBlock(string.Empty),
            ContainerList.Create(
                containers.Containers,
                containers,
                containers.OpenShell,
                pendingRemoval,
                id => setPendingRemoval(id),
                () => setPendingRemoval(null)),
            containers.ShellError is { } shellError
                ? TextBlock(AppStrings.Format("OpenShellError", shellError))
                : TextBlock(string.Empty),
            Button(AppStrings.Get("RefreshButton"), () => _ = polling.RefreshAsync())
        ).Padding(20);
    }
}
