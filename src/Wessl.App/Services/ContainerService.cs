using Microsoft.UI.Reactor;
using Wessl.Domain;

namespace Wessl.App.Services;

public sealed class ContainerService(IWslcClient client)
{
    public IReadOnlyList<ContainerInfo> Containers { get; private set; } = [];

    public string? Error { get; private set; }

    public string? Status { get; private set; }

    public bool IsBusy { get; private set; }

    public event Action? Changed;

    public async Task RefreshAsync()
    {
        var dispatcher = ReactorApp.UIDispatcher
            ?? throw new InvalidOperationException("The UI dispatcher is not available.");
        IReadOnlyList<ContainerInfo>? result = null;
        string? error = null;
        try
        {
            result = await client.ListContainersAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            error = exception.Message;
        }

        dispatcher.TryEnqueue(() =>
        {
            if (result is not null)
            {
                Containers = result;
            }

            Error = error;
            Status = error is null ? "Containers refreshed." : null;
            Changed?.Invoke();
        });
    }

    public Task StartAsync(ContainerInfo container) =>
        RunActionAsync(container, "Starting", client.StartContainerAsync);

    public Task StopAsync(ContainerInfo container) =>
        RunActionAsync(container, "Stopping", client.StopContainerAsync);

    public Task RestartAsync(ContainerInfo container) =>
        RunActionAsync(container, "Restarting", client.RestartContainerAsync);

    public Task RemoveAsync(ContainerInfo container) =>
        RunActionAsync(container, "Removing", client.RemoveContainerAsync);

    private async Task RunActionAsync(
        ContainerInfo container,
        string action,
        Func<string, CancellationToken, Task> execute)
    {
        var dispatcher = ReactorApp.UIDispatcher
            ?? throw new InvalidOperationException("The UI dispatcher is not available.");

        if (IsBusy)
        {
            dispatcher.TryEnqueue(() =>
            {
                Error = "Wait for the current container action to finish.";
                Changed?.Invoke();
            });
            return;
        }

        dispatcher.TryEnqueue(() =>
        {
            IsBusy = true;
            Error = null;
            Status = $"{action} {container.Name}…";
            Changed?.Invoke();
        });

        Exception? actionError = null;
        try
        {
            await execute(container.Id, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            actionError = exception;
        }

        IReadOnlyList<ContainerInfo>? result = null;
        Exception? refreshError = null;
        try
        {
            result = await client.ListContainersAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            refreshError = exception;
        }

        dispatcher.TryEnqueue(() =>
        {
            if (result is not null)
            {
                Containers = result;
            }

            IsBusy = false;
            if (actionError is not null)
            {
                Status = null;
                Error = $"{action} {container.Name} failed: {actionError.Message}";
            }
            else if (refreshError is not null)
            {
                Status = $"{action} {container.Name} succeeded.";
                Error = $"Unable to refresh containers: {refreshError.Message}";
            }
            else
            {
                Status = $"{action} {container.Name} succeeded.";
                Error = null;
            }

            Changed?.Invoke();
        });
    }
}
