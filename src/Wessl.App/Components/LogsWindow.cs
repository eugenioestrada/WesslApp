using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hooks;
using Wessl.App.Services;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public sealed class LogsWindow(
    ContainerLogsSession session,
    Func<Task<string?>> pickSavePath) : Component
{
    public override Element Render()
    {
        var (revision, setRevision) = UseState(0);
        var (searchTerm, setSearchTerm) = UseState(string.Empty);
        var (selectedText, setSelectedText) = UseState(string.Empty);
        var (operationMessage, setOperationMessage) = UseState<string?>(null);
        var logTextBox = this.UseElementRef<TextBox>();
        UseEffect(() =>
        {
            void OnChanged() => setRevision(revision + 1);
            session.Changed += OnChanged;
            return () => session.Changed -= OnChanged;
        }, session, revision);
        UseEffect(() =>
        {
            if (logTextBox.Current is { } textBox)
            {
                textBox.AcceptsReturn = true;
                textBox.TextWrapping = TextWrapping.Wrap;
                if (textBox.SelectionLength == 0)
                {
                    textBox.Select(textBox.Text.Length, 0);
                }
            }
        }, ContainerLogsSession.FilterText(session.Text, searchTerm));

        var visibleText = ContainerLogsSession.FilterText(session.Text, searchTerm);

        void CopyLogs(string text)
        {
            try
            {
                var package = new DataPackage();
                package.SetText(text);
                Clipboard.SetContent(package);
                setOperationMessage(AppStrings.Get("LogsCopied"));
            }
            catch (Exception exception)
            {
                setOperationMessage(AppStrings.Format("LogsCopyError", exception.Message));
            }
        }

        async Task SaveLogsAsync()
        {
            try
            {
                var path = await pickSavePath();
                if (path is null)
                {
                    return;
                }

                await session.SaveToFileAsync(path);
                setOperationMessage(AppStrings.Get("LogsSaved"));
            }
            catch (Exception exception)
            {
                setOperationMessage(AppStrings.Format("LogsSaveError", exception.Message));
            }
        }

        Element operationInfo = operationMessage is { } message
            ? InfoBar(message).Success() with { IsOpen = true, IsClosable = true }
            : TextBlock(string.Empty);
        Element logContent = visibleText.Length == 0 && session.Text.Length > 0
            ? TextBlock(AppStrings.Get("LogsNoMatches")).Grid(row: 4)
            : TextBox(visibleText, _ => { })
                .IsReadOnly(true)
                .AutomationName(AppStrings.Get("LogsContentLabel"))
                .SelectionChanged((text, _, _) => setSelectedText(text))
                .Ref(logTextBox)
                .Grid(row: 4);

        return Grid(
            columns: [GridSize.Star()],
            rows: [GridSize.Auto, GridSize.Auto, GridSize.Auto, GridSize.Auto, GridSize.Star()],
            (session.Error is { } error
                ? InfoBar(AppStrings.Format(error)).Error() with { IsOpen = true, IsClosable = false }
                : InfoBar(AppStrings.Get(
                    session.IsTruncated
                        ? session.IsFollowing ? "LogsFollowingTruncated" : "LogsEndedTruncated"
                        : session.IsFollowing ? "LogsFollowing" : "LogsEnded"))
                    .Informational() with { IsOpen = true, IsClosable = false })
                .Grid(row: 0),
            operationInfo.Grid(row: 1),
            Grid(
                columns: [GridSize.Star()],
                rows: [GridSize.Auto, GridSize.Auto],
                TextBlock(AppStrings.Get("LogsSearchLabel")).Grid(row: 0),
                TextBox(searchTerm, setSearchTerm)
                    .AutomationName(AppStrings.Get("LogsSearchLabel"))
                    .Grid(row: 1))
                .Grid(row: 2),
            HStack(8,
                Button(AppStrings.Get("LogsCopySelected"), () => CopyLogs(selectedText))
                    .IsEnabled(selectedText.Length > 0),
                Button(AppStrings.Get("LogsCopyVisible"), () => CopyLogs(visibleText))
                    .IsEnabled(visibleText.Length > 0),
                Button(AppStrings.Get("LogsSaveButton"), () => _ = SaveLogsAsync())
                    .IsEnabled(session.Text.Length > 0))
                .Grid(row: 3),
            logContent
        ).Padding(16);
    }
}
