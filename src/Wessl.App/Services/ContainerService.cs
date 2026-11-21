using Wessl.Domain;

namespace Wessl.App.Services;

public sealed class ContainerService(
    IWslcClient client,
    Action<Action> dispatch,
    IContainerShellLauncher shellLauncher,
    AppSettings? settings = null)
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly object _usageRequestLock = new();
    private readonly HashSet<string> _usageRequests = [];
    private readonly HashSet<string> _usageLoading = [];
    private readonly Dictionary<string, ContainerUsage> _usage = [];
    private readonly Dictionary<string, LocalizedMessage> _usageErrors = [];
    private int _actionInProgress;
    private int _statsInProgress;
    private int _statsRevision;

    public IReadOnlyList<ContainerInfo> Containers { get; private set; } = [];

    public bool HasRunningContainers => Containers.Any(container => container.State == ContainerState.Running);

    public bool HasRefreshed { get; private set; }

    public bool IsWslcUnavailable { get; private set; }

    public LocalizedMessage? Error { get; private set; }

    public LocalizedMessage? Status { get; private set; }

    public bool IsBusy => Volatile.Read(ref _actionInProgress) != 0;

    public LocalizedMessage? ShellError { get; private set; }

    public IReadOnlyDictionary<string, ContainerUsage> Usage => _usage;

    public IReadOnlySet<string> UsageLoading => _usageLoading;

    public IReadOnlyDictionary<string, LocalizedMessage> UsageErrors => _usageErrors;

    public event Action? Changed;

    public async Task RefreshUsagesAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested
            || Interlocked.CompareExchange(ref _statsInProgress, 1, 0) != 0)
        {
            return;
        }

        var ids = Containers.Where(container => container.State == ContainerState.Running)
            .Select(container => container.Id).ToArray();
        if (ids.Length == 0)
        {
            Interlocked.Exchange(ref _statsInProgress, 0);
            return;
        }
        var revision = Interlocked.Increment(ref _statsRevision);
        foreach (var id in ids.Where(id => !_usage.ContainsKey(id)))
        {
            _usageLoading.Add(id);
        }
        Changed?.Invoke();

        IReadOnlyDictionary<string, ContainerUsage>? result = null;
        Exception? error = null;
        try
        {
            result = await client.GetContainerUsagesAsync(ids, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            error = exception;
        }
        finally
        {
            Interlocked.Exchange(ref _statsInProgress, 0);
        }

        dispatch(() =>
        {
            if (cancellationToken.IsCancellationRequested || revision != Volatile.Read(ref _statsRevision))
            {
                return;
            }
            foreach (var id in ids)
            {
                _usageLoading.Remove(id);
                if (!Containers.Any(container => container.Id == id && container.State == ContainerState.Running))
                {
                    continue;
                }
                if (error is not null)
                {
                    _usageErrors[id] = new("ContainerUsageError", [error.Message]);
                }
                else if (result is not null && result.TryGetValue(id, out var usage))
                {
                    _usage[id] = usage;
                    _usageErrors.Remove(id);
                }
                else
                {
                    _usage.Remove(id);
                    _usageErrors[id] = new("ContainerUsageUnavailable", []);
                }
            }
            Changed?.Invoke();
        });
    }

    public void StopUsagePolling()
    {
        Interlocked.Increment(ref _statsRevision);
        var changed = _usageLoading.Count > 0 || _usage.Count > 0 || _usageErrors.Count > 0;
        _usageLoading.Clear();
        _usage.Clear();
        _usageErrors.Clear();
        if (changed)
        {
            Changed?.Invoke();
        }
    }

    public async Task RefreshUsageAsync(ContainerInfo container)
    {
        if (container.State != ContainerState.Running
            || !Containers.Any(item => item.Id == container.Id && item.State == ContainerState.Running))
        {
            return;
        }

        lock (_usageRequestLock)
        {
            if (!_usageRequests.Add(container.Id))
            {
                return;
            }
        }

        dispatch(() =>
        {
            _usageLoading.Add(container.Id);
            _usage.Remove(container.Id);
            _usageErrors.Remove(container.Id);
            Changed?.Invoke();
        });

        ContainerUsage? usage = null;
        Exception? error = null;
        try
        {
            usage = await client.GetContainerUsageAsync(container.Id).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            error = exception;
        }
        finally
        {
            lock (_usageRequestLock)
            {
                _usageRequests.Remove(container.Id);
            }
        }

        dispatch(() =>
        {
            _usageLoading.Remove(container.Id);
            if (Containers.Any(item => item.Id == container.Id && item.State == ContainerState.Running))
            {
                if (usage is not null)
                {
                    _usage[container.Id] = usage;
                }
                else if (error is not null)
                {
                    _usageErrors[container.Id] = new("ContainerUsageError", [error.Message]);
                }
            }

            Changed?.Invoke();
        });
    }

    public void OpenShell(ContainerInfo container)
    {
        ShellError = null;
        if (container.State != ContainerState.Running)
        {
            ShellError = new("ShellRequiresRunningContainer", []);
            Changed?.Invoke();
            return;
        }

        try
        {
            shellLauncher.OpenShell(
                container.Id,
                settings?.DefaultTerminal ?? ContainerTerminal.WindowsTerminal);
        }
        catch (Exception exception)
        {
            ShellError = new("OpenShellError", [exception.Message]);
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
                SetContainers(result);
                HasRefreshed = true;
            }
            else if (unavailable)
            {
                SetContainers([]);
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
                    new LocalizedMessageList(failures.Select(failure =>
                        new LocalizedMessage("TrayStopAllFailureDetail", [failure.Name, failure.Message])).ToArray())
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
                    SetContainers(result);
                    HasRefreshed = true;
                }
                else if (refreshError is WslcUnavailableException)
                {
                    SetContainers([]);
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
                    SetContainers(result);
                    HasRefreshed = true;
                }
                else if (refreshError is WslcUnavailableException)
                {
                    SetContainers([]);
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

    private void SetContainers(IReadOnlyList<ContainerInfo> containers)
    {
        Containers = containers;
        var runningIds = containers
            .Where(container => container.State == ContainerState.Running)
            .Select(container => container.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var id in _usage.Keys.Where(id => !runningIds.Contains(id)).ToArray())
        {
            _usage.Remove(id);
        }

        foreach (var id in _usageErrors.Keys.Where(id => !runningIds.Contains(id)).ToArray())
        {
            _usageErrors.Remove(id);
        }
    }
}

public sealed record LocalizedMessage(string ResourceKey, object?[] Arguments);

public sealed record LocalizedMessageList(IReadOnlyList<LocalizedMessage> Messages);

public sealed record StopAllResult(LocalizedMessage? Status, LocalizedMessage? Error);
