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
                ? TextBlock(AppStrings.Format("RefreshError", error))
                : ContainerList.Create(containers.Containers),
            Button(AppStrings.Get("RefreshButton"), () => _ = polling.RefreshAsync())
        ).Padding(20);
    }
}
