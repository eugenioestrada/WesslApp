using Wessl.Domain;

namespace Wessl.App.Services;

public sealed class ImageService(IImageClient client, Action<Action> dispatch)
{
    private int _busy;

    public IReadOnlyList<ImageInfo> Images { get; private set; } = [];
    public bool HasLoaded { get; private set; }
    public bool IsBusy => Volatile.Read(ref _busy) != 0;
    public LocalizedMessage? Error { get; private set; }
    public LocalizedMessage? Status { get; private set; }
    public event Action? Changed;

    public Task RefreshAsync() => RunAsync(prune: false);

    public Task PruneAsync() => RunAsync(prune: true);

    private async Task RunAsync(bool prune)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            return;
        }

        dispatch(() =>
        {
            Error = null;
            Status = new(prune ? "ImagesPrunePending" : "ImagesLoading", []);
            Changed?.Invoke();
        });

        string? output = null;
        Exception? pruneError = null;
        Exception? refreshError = null;
        IReadOnlyList<ImageInfo>? images = null;
        if (prune)
        {
            try
            {
                output = await client.PruneImagesAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                pruneError = exception;
            }
        }

        try
        {
            images = await client.ListImagesAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            refreshError = exception;
        }

        dispatch(() =>
        {
            if (images is not null)
            {
                Images = images;
                HasLoaded = true;
            }

            var refreshFailure = refreshError is null ? null : new LocalizedMessage("ImagesRefreshError", [refreshError.Message]);
            var pruneFailure = pruneError is null ? null : new LocalizedMessage("ImagesPruneError", [pruneError.Message]);
            Error = pruneFailure is not null && refreshFailure is not null
                ? new("ActionAndRefreshError", [pruneFailure, refreshFailure])
                : pruneFailure ?? refreshFailure;
            Status = prune && pruneError is null
                ? new("ImagesPruneSuccess", [string.IsNullOrWhiteSpace(output) ? AppOutputUnavailable() : output.Trim()])
                : null;
            Interlocked.Exchange(ref _busy, 0);
            Changed?.Invoke();
        });
    }

    private static LocalizedMessage AppOutputUnavailable() => new("ImagesPruneNoDetails", []);
}
