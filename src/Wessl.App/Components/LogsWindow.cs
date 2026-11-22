using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hooks;
using Microsoft.UI.Reactor.Layout;
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
        var (operationMessage, setOperationMessage) = UseState<LogOperationResult?>(null);
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

        async Task CopyLogsAsync(string text)
        {
            var result = await LogOperation.RunAsync(
                () =>
                {
                    var package = new DataPackage();
                    package.SetText(text);
                    Clipboard.SetContent(package);
                    return Task.CompletedTask;
                },
                AppStrings.Get("LogsCopied"),
                exception => AppStrings.Format("LogsCopyError", exception.Message));
            setOperationMessage(result);
        }

        async Task SaveLogsAsync()
        {
            var path = await pickSavePath();
            if (path is null)
            {
                return;
            }

            var result = await LogOperation.RunAsync(
                () => session.SaveToFileAsync(path),
                AppStrings.Get("LogsSaved"),
                exception => AppStrings.Format("LogsSaveError", exception.Message));
            setOperationMessage(result);
        }

        var rows = new List<GridSize>();
        var content = new List<Element>();
        var row = 0;
        var statusKey = session.IsTruncated
            ? session.IsFollowing ? "LogsFollowingTruncatedStatus" : "LogsEndedTruncatedStatus"
            : session.IsFollowing ? "LogsFollowingStatus" : "LogsEndedStatus";
        var statusInfo = session.Error is { } error
            ? InfoBar(message: AppStrings.Format(error)).Error() with { IsOpen = true, IsClosable = false }
            : InfoBar(message: AppStrings.Get(statusKey))
                .Informational() with { IsOpen = true, IsClosable = false };
        content.Add(statusInfo.Grid(row: row++));
        rows.Add(GridSize.Auto);

        if (operationMessage is { } message)
        {
            var operationInfo = InfoBar(message: message.Message);
            content.Add(((message.IsSuccess ? operationInfo.Success() : operationInfo.Error())
                    with { IsOpen = true, IsClosable = true })
                .Closed(() => setOperationMessage(null))
                .Grid(row: row++));
            rows.Add(GridSize.Auto);
        }

        content.Add(Grid(
                columns: [GridSize.Star().MinSize(0)],
                rows: [GridSize.Auto, GridSize.Auto],
                TextBlock(AppStrings.Get("LogsSearchLabel")).Grid(row: 0),
                TextBox(searchTerm, setSearchTerm)
                    .AutomationName(AppStrings.Get("LogsSearchLabel"))
                    .Grid(row: 1))
            .Grid(row: row++));
        rows.Add(GridSize.Auto);

        content.Add((FlexRow(
                Button(AppStrings.Get("LogsCopySelected"), () => _ = CopyLogsAsync(selectedText))
                    .IsEnabled(selectedText.Length > 0),
                Button(AppStrings.Get("LogsCopyVisible"), () => _ = CopyLogsAsync(visibleText))
                    .IsEnabled(visibleText.Length > 0),
                Button(AppStrings.Get("LogsSaveButton"), () => _ = SaveLogsAsync())
                    .IsEnabled(session.Text.Length > 0))
            with
            {
                Wrap = FlexWrap.Wrap,
                ColumnGap = 8,
                RowGap = 8
            }).Grid(row: row++));
        rows.Add(GridSize.Auto);

        Element logContent = visibleText.Length == 0 && session.Text.Length > 0
            ? TextBlock(AppStrings.Get("LogsNoMatches"))
            : TextBox(visibleText, _ => { })
                .IsReadOnly(true)
                .AutomationName(AppStrings.Get("LogsContentLabel"))
                .SelectionChanged((text, _, _) => setSelectedText(text))
                .Ref(logTextBox);
        content.Add(logContent.Grid(row: row));
        rows.Add(GridSize.Star().MinSize(0));

        return Grid(
            columns: [GridSize.Star().MinSize(0)],
            rows: rows.ToArray(),
            content.ToArray())
            .Padding(16);
    }
}
