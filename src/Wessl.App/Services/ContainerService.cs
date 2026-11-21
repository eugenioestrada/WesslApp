using Wessl.Domain;

namespace Wessl.App.Services;

public sealed class ContainerService(
    IWslcClient client,
    Action<Action> dispatch,
    IContainerShellLauncher shellLauncher,
    AppSettings? settings = null)
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private int _actionInProgress;

    public IReadOnlyList<ContainerInfo> Containers { get; private set; } = [];

    public bool HasRunningContainers => Containers.Any(container => container.State == ContainerState.Running);

    public bool HasRefreshed { get; private set; }

    public bool IsWslcUnavailable { get; private set; }

    public LocalizedMessage? Error { get; private set; }

    public LocalizedMessage? Status { get; private set; }

    public bool IsBusy => Volatile.Read(ref _actionInProgress) != 0;

    public string? ShellError { get; private set; }

    public event Action? Changed;

    public void OpenShell(ContainerInfo container)
    {
        ShellError = null;
        try
        {
            if (container.State != ContainerState.Running)
            {
                throw new InvalidOperationException("A shell can only be opened for a running container.");
            }

            shellLauncher.OpenShell(
                container.Id,
                settings?.DefaultTerminal ?? ContainerTerminal.WindowsTerminal);
        }
        catch (Exception exception)
        {
            ShellError = exception.Message;
        }

        Changed?.Invoke();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default, bool skipIfBusy = false)
    {
        try
        {
            if (skipIfBusy)
            {
                if (!await _refreshLock.WaitAsync(0, cancellationToken))
                {
                    return;
                }
            }
            else
            {
                await _refreshLock.WaitAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        IReadOnlyList<ContainerInfo>? result = null;
        LocalizedMessage? error = null;
        var unavailable = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            result = await client.ListContainersAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            unavailable = exception is WslcUnavailableException;
            error = new("RefreshError", [exception.Message]);
        }
        finally
        {
            _refreshLock.Release();
        }

        dispatch(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (result is not null)
            {
                Containers = result;
                HasRefreshed = true;
            }
            else if (unavailable)
            {
                Containers = [];
                HasRefreshed = false;
            }

            IsWslcUnavailable = unavailable;
            Error = error;
            Changed?.Invoke();
        });
    }

    public Task StartAsync(ContainerInfo container) =>
        RunActionAsync(container, "Start", client.StartContainerAsync);

    public Task StopAsync(ContainerInfo container) =>
        RunActionAsync(container, "Stop", client.StopContainerAsync);

    public Task<StopAllResult> StopAllAsync() => StopAllAsync(Containers);

    public async Task<StopAllResult> StopAllAsync(IReadOnlyCollection<ContainerInfo> containersToStop)
    {
        ArgumentNullException.ThrowIfNull(containersToStop);
        if (Interlocked.CompareExchange(ref _actionInProgress, 1, 0) != 0)
        {
            dispatch(() =>
            {
                Error = new("WaitForCurrentAction", []);
                Changed?.Invoke();
            });
            return new(null, new("WaitForCurrentAction", []));
        }

        try
        {
            var running = containersToStop
                .Where(container => container.State == ContainerState.Running)
                .ToArray();
            dispatch(() =>
            {
                Error = null;
                Status = new("TrayStopAllPending", [running.Length]);
                Changed?.Invoke();
            });

            var failures = new List<(string Name, string Message)>();
            var stopped = 0;
            Exception? refreshError = null;
            IReadOnlyList<ContainerInfo>? result = null;
            await _refreshLock.WaitAsync();
            try
            {
                foreach (var container in running)
                {
                    try
                    {
                        await client.StopContainerAsync(container.Id).ConfigureAwait(false);
                        stopped++;
                    }
                    catch (Exception exception)
                    {
                        failures.Add((container.Name, exception.Message));
                    }
                }

                try
                {
                    result = await client.ListContainersAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    refreshError = exception;
                }
            }
            finally
            {
                _refreshLock.Release();
            }

            var completedStatus = new LocalizedMessage("TrayStopAllCompleted", [stopped, running.Length]);
            LocalizedMessage? completedError = failures.Count > 0
                ? new("TrayStopAllPartialFailure", [
                    string.Join("; ", failures.Select(failure => $"{failure.Name}: {failure.Message}"))
                ])
                : null;
            if (refreshError is not null)
            {
                var refreshMessage = new LocalizedMessage("RefreshError", [refreshError.Message]);
                completedError = completedError is null
                    ? refreshMessage
                    : new("ActionAndRefreshError", [completedError, refreshMessage]);
            }

            dispatch(() =>
            {
                if (result is not null)
                {
                    Containers = result;
                    HasRefreshed = true;
                }
                else if (refreshError is WslcUnavailableException)
                {
                    Containers = [];
                    HasRefreshed = false;
                }

                IsWslcUnavailable = refreshError is WslcUnavailableException;
                Status = completedStatus;
                Error = completedError;
                Changed?.Invoke();
            });
            return new(completedStatus, completedError);
        }
        finally
        {
            Interlocked.Exchange(ref _actionInProgress, 0);
        }
    }

    public Task RestartAsync(ContainerInfo container) =>
        RunActionAsync(container, "Restart", client.RestartContainerAsync);

    public Task RemoveAsync(ContainerInfo container) =>
        RunActionAsync(container, "Remove", client.RemoveContainerAsync);

    private async Task RunActionAsync(
        ContainerInfo container,
        string action,
        Func<string, CancellationToken, Task> execute)
    {
        if (Interlocked.CompareExchange(ref _actionInProgress, 1, 0) != 0)
        {
            dispatch(() =>
            {
                Error = new("WaitForCurrentAction", []);
                Changed?.Invoke();
            });
            return;
        }

        try
        {
            dispatch(() =>
            {
                Error = null;
                Status = new($"Action{action}Pending", [container.Name]);
                Changed?.Invoke();
            });

            Exception? actionError = null;
            Exception? refreshError = null;
            IReadOnlyList<ContainerInfo>? result = null;
            await _refreshLock.WaitAsync();
            try
            {
                try
                {
                    await execute(container.Id, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    actionError = exception;
                }

                try
                {
                    result = await client.ListContainersAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    refreshError = exception;
                }
            }
            finally
            {
                _refreshLock.Release();
            }

            dispatch(() =>
            {
                if (result is not null)
                {
                    Containers = result;
                    HasRefreshed = true;
                }
                else if (refreshError is WslcUnavailableException)
                {
                    Containers = [];
                    HasRefreshed = false;
                }

                IsWslcUnavailable = refreshError is WslcUnavailableException;

                if (actionError is not null)
                {
                    Status = null;
                    var actionFailure = new LocalizedMessage(
                        $"Action{action}Failed",
                        [container.Name, actionError.Message]);
                    Error = refreshError is null
                        ? actionFailure
                        : new(
                            "ActionAndRefreshError",
                            [actionFailure, new LocalizedMessage("RefreshError", [refreshError.Message])]);
                }
                else if (refreshError is not null)
                {
                    Status = new($"Action{action}Succeeded", [container.Name]);
                    Error = new("RefreshError", [refreshError.Message]);
                }
                else
                {
                    Status = new($"Action{action}Succeeded", [container.Name]);
                    Error = null;
                }

                Changed?.Invoke();
            });
        }
        finally
        {
            Interlocked.Exchange(ref _actionInProgress, 0);
        }
    }
}

public sealed record LocalizedMessage(string ResourceKey, object?[] Arguments);

public sealed record StopAllResult(LocalizedMessage? Status, LocalizedMessage? Error);
