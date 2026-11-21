using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public static class TrayMenu
{
    public static Element Create(Action open, Action refresh, Action exit) =>
        VStack(4,
            Button("Open Wessl", open),
            Button("Refresh", refresh),
            Button("Exit", exit)).Padding(8);
}
