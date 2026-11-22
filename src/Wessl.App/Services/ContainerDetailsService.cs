using Wessl.Domain;

namespace Wessl.App.Services;

public sealed class ContainerDetailsService(IWslcClient client, Action<Action> dispatch)
{
    private int _revision;

    public ContainerInfo? Container { get; private set; }

    public ContainerDetails? Details { get; private set; }

    public bool IsLoading { get; private set; }

    public Exception? Error { get; private set; }

    public event Action? Changed;

    public async Task InspectAsync(ContainerInfo container, CancellationToken cancellationToken = default)
    {
        var revision = Interlocked.Increment(ref _revision);
        dispatch(() =>
        {
            Container = container;
            Details = null;
            Error = null;
            IsLoading = true;
            Changed?.Invoke();
        });

        ContainerDetails? details = null;
        Exception? error = null;
        try
        {
            details = await client.GetContainerDetailsAsync(container.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            error = exception;
        }

        dispatch(() =>
        {
            if (revision != Volatile.Read(ref _revision))
            {
                return;
            }

            Details = details;
            Error = error;
            IsLoading = false;
            Changed?.Invoke();
        });
    }

    public void Clear()
    {
        Interlocked.Increment(ref _revision);
        dispatch(() =>
        {
            Container = null;
            Details = null;
            Error = null;
            IsLoading = false;
            Changed?.Invoke();
        });
    }
}
