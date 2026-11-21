using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hooks;
using Wessl.App.Services;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public sealed class LogsWindow(ContainerLogsSession session) : Component
{
    public override Element Render()
    {
        var (revision, setRevision) = UseState(0);
        var scroll = this.UseElementRef<Microsoft.UI.Xaml.Controls.ScrollViewer>();
        UseEffect(() =>
        {
            void OnChanged() => setRevision(revision + 1);
            session.Changed += OnChanged;
            return () => session.Changed -= OnChanged;
        }, session, revision);
        UseEffect(() =>
        {
            if (scroll.Current is { } viewer)
            {
                viewer.UpdateLayout();
                viewer.ChangeView(null, viewer.ScrollableHeight, null, true);
            }
        }, session.Text);

        return Grid(
            columns: [GridSize.Star()],
            rows: [GridSize.Auto, GridSize.Star()],
            (session.Error is { } error
                ? TextBlock(AppStrings.Format(error)).TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap)
                : TextBlock(AppStrings.Get(session.IsFollowing ? "LogsFollowing" : "LogsEnded"))).Grid(row: 0),
            ScrollViewer(TextBlock(session.Text).TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap))
                .Ref(scroll).Grid(row: 1)
        ).Padding(16);
    }
}
