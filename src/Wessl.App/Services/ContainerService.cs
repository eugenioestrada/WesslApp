using Microsoft.UI.Reactor;
using Wessl.Domain;

namespace Wessl.App.Services;

public sealed class ContainerService(IWslcClient client, IContainerShellLauncher shellLauncher)
{
    public IReadOnlyList<ContainerInfo> Containers { get; private set; } = [];

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
            Changed?.Invoke();
        });
    }
}
