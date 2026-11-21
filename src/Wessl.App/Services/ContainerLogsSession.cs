using Wessl.Domain;

namespace Wessl.App.Services;

public sealed class ContainerLogsSession(IContainerLogsClient client, Action<Action> dispatch)
{
    private const int MaxCharacters = 128 * 1024;
    private readonly CancellationTokenSource _stop = new();

    public string Text { get; private set; } = string.Empty;
    public LocalizedMessage? Error { get; private set; }
    public bool IsFollowing { get; private set; } = true;
    public event Action? Changed;

    public void Start(ContainerInfo container)
    {
        if (container.State != ContainerState.Running)
        {
            IsFollowing = false;
            Error = new("LogsNotRunning", []);
            Changed?.Invoke();
            return;
        }

        _ = Task.Run(() => FollowAsync(container.Id));
    }

    public void Stop() => _stop.Cancel();

    private async Task FollowAsync(string id)
    {
        try
        {
            await foreach (var line in client.FollowLogsAsync(id, _stop.Token).ConfigureAwait(false))
            {
                var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                dispatch(() =>
                {
                    if (!_stop.IsCancellationRequested)
                    {
                        var text = Text + line + Environment.NewLine;
                        Text = text.Length > MaxCharacters ? text[^MaxCharacters..] : text;
                        Changed?.Invoke();
                    }
                    delivered.TrySetResult();
                });
                await delivered.Task.WaitAsync(_stop.Token).ConfigureAwait(false);
            }
            dispatch(() =>
            {
                if (!_stop.IsCancellationRequested)
                {
                    IsFollowing = false;
                    Changed?.Invoke();
                }
            });
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            dispatch(() =>
            {
                if (!_stop.IsCancellationRequested)
                {
                    IsFollowing = false;
                    Error = new("LogsError", [exception.Message]);
                    Changed?.Invoke();
                }
            });
        }
    }
}
