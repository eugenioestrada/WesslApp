using Wessl.Domain;

namespace Wessl.App.Services;

public sealed class ContainerService(
    IWslcClient client,
    Action<Action> dispatch,
    IContainerShellLauncher shellLauncher)
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public IReadOnlyList<ContainerInfo> Containers { get; private set; } = [];

    public bool HasRunningContainers => Containers.Any(container => container.State == ContainerState.Running);

    public bool HasRefreshed { get; private set; }

    public string? Error { get; private set; }

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

            shellLauncher.OpenShell(container.Id);
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
        string? error = null;
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
            error = exception.Message;
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

            Error = error;
            Changed?.Invoke();
        });
    }
}
