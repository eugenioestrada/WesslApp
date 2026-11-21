using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public sealed class TrayMenuHost : Component
{
    public override Element Render() =>
        TextBlock(string.Empty).Width(1).Height(1);
}
