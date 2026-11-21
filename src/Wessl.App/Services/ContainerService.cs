using Microsoft.UI.Reactor;
using Wessl.Domain;

namespace Wessl.App.Services;

public sealed class ContainerService(IWslcClient client)
{
    public IReadOnlyList<ContainerInfo> Containers { get; private set; } = [];

    public string? Error { get; private set; }

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
            Changed?.Invoke();
        });
    }
}
