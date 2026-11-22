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

    public LocalizedMessage? DiagnosticsError { get; private set; }

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

        ContainerRefreshResult? result = null;
        LocalizedMessage? error = null;
        var unavailable = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            result = await RefreshContainerListAsync(cancellationToken).ConfigureAwait(false);
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
                SetContainers(result.Containers);
                DiagnosticsError = result.DiagnosticsError;
                HasRefreshed = true;
            }
            else if (unavailable)
            {
                SetContainers([]);
                DiagnosticsError = null;
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

    public Task<BulkActionResult> StartSelectedAsync(IReadOnlyCollection<ContainerInfo> containers) =>
        RunBulkActionAsync(containers, "Start", ContainerState.Stopped, client.StartContainerAsync);

    public Task<BulkActionResult> RestartSelectedAsync(IReadOnlyCollection<ContainerInfo> containers) =>
        RunBulkActionAsync(containers, "Restart", ContainerState.Running, client.RestartContainerAsync);

    public Task<BulkActionResult> RemoveSelectedAsync(IReadOnlyCollection<ContainerInfo> containers) =>
        RunBulkActionAsync(containers, "Remove", ContainerState.Stopped, client.RemoveContainerAsync);

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
            ContainerRefreshResult? result = null;
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
                    result = await RefreshContainerListAsync(CancellationToken.None).ConfigureAwait(false);
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
                    SetContainers(result.Containers);
                    DiagnosticsError = result.DiagnosticsError;
                    HasRefreshed = true;
                }
                else if (refreshError is WslcUnavailableException)
                {
                    SetContainers([]);
                    DiagnosticsError = null;
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
            ReleaseAction();
        }
    }

    public Task RestartAsync(ContainerInfo container) =>
        RunActionAsync(container, "Restart", client.RestartContainerAsync);

    public Task RemoveAsync(ContainerInfo container) =>
        RunActionAsync(container, "Remove", client.RemoveContainerAsync);

    private async Task<BulkActionResult> RunBulkActionAsync(
        IReadOnlyCollection<ContainerInfo> selectedContainers,
        string action,
        ContainerState requiredState,
        Func<string, CancellationToken, Task> execute)
    {
        ArgumentNullException.ThrowIfNull(selectedContainers);
        var selected = selectedContainers
            .DistinctBy(container => container.Id, StringComparer.Ordinal)
            .ToArray();
        if (selected.Length == 0)
        {
            return new(null, null);
        }

        if (Interlocked.CompareExchange(ref _actionInProgress, 1, 0) != 0)
        {
            var busyError = new LocalizedMessage("WaitForCurrentAction", []);
            dispatch(() =>
            {
                Error = busyError;
                Changed?.Invoke();
            });
            return new(null, busyError);
        }

        try
        {
            var pendingKey = action switch
            {
                "Start" => "BulkStartPending",
                "Restart" => "BulkRestartPending",
                "Remove" => "BulkRemovePending",
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            };
            dispatch(() =>
            {
                Error = null;
                Status = new(pendingKey, [selected.Length]);
                Changed?.Invoke();
            });

            var successes = new List<LocalizedMessage>();
            var failures = new List<LocalizedMessage>();
            Exception? verificationError = null;
            Exception? refreshError = null;
            IReadOnlyList<ContainerInfo>? result = null;
            await _refreshLock.WaitAsync();
            try
            {
                IReadOnlyList<ContainerInfo>? currentContainers = null;
                if (action == "Remove")
                {
                    currentContainers = [];
                }
                else
                {
                    try
                    {
                        currentContainers = await client.ListContainersAsync().ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        verificationError = exception;
                        foreach (var container in selected)
                        {
                            failures.Add(new("BulkActionVerificationFailed", [container.Name, exception.Message]));
                        }
                    }
                }

                if (currentContainers is not null)
                {
                    foreach (var container in selected)
                    {
                        IReadOnlyList<ContainerInfo> actionContainers = currentContainers;
                        if (action == "Remove")
                        {
                            try
                            {
                                actionContainers = await client.ListContainersAsync().ConfigureAwait(false);
                            }
                            catch (Exception exception)
                            {
                                verificationError ??= exception;
                                failures.Add(new("BulkActionVerificationFailed", [container.Name, exception.Message]));
                                continue;
                            }
                        }

                        var current = actionContainers.FirstOrDefault(item =>
                            string.Equals(item.Id, container.Id, StringComparison.Ordinal));
                        if (current is null)
                        {
                            failures.Add(new("BulkActionNotFound", [container.Name]));
                            continue;
                        }

                        if (current.State != requiredState)
                        {
                            failures.Add(new("BulkActionStateChanged", [container.Name]));
                            continue;
                        }

                        try
                        {
                            await execute(container.Id, CancellationToken.None).ConfigureAwait(false);
                            successes.Add(new("BulkActionItemSucceeded", [container.Name]));
                        }
                        catch (Exception exception)
                        {
                            failures.Add(new("BulkActionFailureDetail", [container.Name, exception.Message]));
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
            }
            finally
            {
                _refreshLock.Release();
            }

            var completedStatus = new LocalizedMessage(
                "BulkActionCompleted",
                [successes.Count, selected.Length, new LocalizedMessageList(successes)]);
            LocalizedMessage? completedError = failures.Count > 0
                ? new("BulkActionPartialFailure", [new LocalizedMessageList(failures)])
                : null;
            if (refreshError is not null)
            {
                var refreshMessage = new LocalizedMessage("RefreshError", [refreshError.Message]);
                completedError = completedError is null
                    ? refreshMessage
                    : new("ActionAndRefreshError", [completedError, refreshMessage]);
            }

            var isUnavailable = result is null
                && (verificationError is WslcUnavailableException || refreshError is WslcUnavailableException);
            dispatch(() =>
            {
                if (result is not null)
                {
                    SetContainers(result);
                    HasRefreshed = true;
                }
                else if (isUnavailable)
                {
                    SetContainers([]);
                    HasRefreshed = false;
                }

                IsWslcUnavailable = isUnavailable;
                Status = completedStatus;
                Error = completedError;
                Changed?.Invoke();
            });
            return new(completedStatus, completedError);
        }
        finally
        {
            ReleaseAction();
        }
    }

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
            ContainerRefreshResult? result = null;
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
                    result = await RefreshContainerListAsync(CancellationToken.None).ConfigureAwait(false);
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
                    SetContainers(result.Containers);
                    DiagnosticsError = result.DiagnosticsError;
                    HasRefreshed = true;
                }
                else if (refreshError is WslcUnavailableException)
                {
                    SetContainers([]);
                    DiagnosticsError = null;
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
            ReleaseAction();
        }
    }

    private void ReleaseAction()
    {
        Interlocked.Exchange(ref _actionInProgress, 0);
        dispatch(() => Changed?.Invoke());
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

    private async Task<ContainerRefreshResult> RefreshContainerListAsync(CancellationToken cancellationToken)
    {
        var containers = await client.ListContainersAsync(cancellationToken).ConfigureAwait(false);
        if (containers.Count == 0)
        {
            return new(containers, null);
        }

        IReadOnlyDictionary<string, ContainerDiagnostics> diagnostics;
        try
        {
            diagnostics = await client.GetContainerDiagnosticsAsync(
                containers.Select(container => container.Id).ToArray(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var unavailable = containers
                .Select(container => container with { Diagnostics = ContainerDiagnostics.Unavailable })
                .ToArray();
            return new(unavailable, new("ContainerDiagnosticsLoadError", [exception.Message]));
        }

        var enriched = containers
            .Select(container => container with
            {
                Diagnostics = diagnostics.TryGetValue(container.Id, out var value)
                    ? value
                    : ContainerDiagnostics.Unavailable
            })
            .ToArray();
        return new(enriched, null);
    }

    private sealed record ContainerRefreshResult(
        IReadOnlyList<ContainerInfo> Containers,
        LocalizedMessage? DiagnosticsError);
}

public sealed record LocalizedMessage(string ResourceKey, object?[] Arguments);

public sealed record LocalizedMessageList(IReadOnlyList<LocalizedMessage> Messages);

public sealed record StopAllResult(LocalizedMessage? Status, LocalizedMessage? Error);

public sealed record BulkActionResult(LocalizedMessage? Status, LocalizedMessage? Error);
