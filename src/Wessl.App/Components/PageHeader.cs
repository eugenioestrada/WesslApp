using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hooks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wessl.App.Services;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public sealed record PageHeaderProps(string Title, Action Back);

public sealed class PageHeader : Component<PageHeaderProps>
{
    public static Element Create(string title, Action back) =>
        Component<PageHeader, PageHeaderProps>(new(title, back));

    public override Element Render()
    {
        var backButton = this.UseElementRef<Button>();
        UseEffect(() =>
        {
            backButton.Current?.Focus(FocusState.Programmatic);
        }, Array.Empty<object>());

        return HStack(12,
            TextBlock(Props.Title).FontSize(24),
            Button(AppStrings.Get("SettingsBackButton"), Props.Back)
                .Ref(backButton));
    }
}
