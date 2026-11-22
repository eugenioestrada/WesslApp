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

    public Task RefreshAsync() => RunAsync();

    public Task PruneAsync() => RunAsync(
        "ImagesPrunePending",
        "ImagesPruneError",
        "ImagesPruneSuccess",
        [],
        () => client.PruneImagesAsync(),
        output => [string.IsNullOrWhiteSpace(output) ? AppOutputUnavailable() : output.Trim()]);

    public Task PullImageAsync(string image)
    {
        if (!IsValidImageReference(image))
        {
            return ReportInvalidImageReference();
        }

        return RunAsync(
            "ImagesPullPending",
            "ImagesPullError",
            "ImagesPullSuccess",
            [image],
            () => client.PullImageAsync(image),
            output => [image, string.IsNullOrWhiteSpace(output) ? AppOutputUnavailable() : output.Trim()]);
    }

    public Task TagImageAsync(string source, string target)
    {
        if (!IsValidImageReference(source) || !IsValidImageReference(target))
        {
            return ReportInvalidImageReference();
        }

        return RunAsync(
            "ImagesTagPending",
            "ImagesTagError",
            "ImagesTagSuccess",
            [source, target],
            () => client.TagImageAsync(source, target),
            output => [source, target, string.IsNullOrWhiteSpace(output) ? AppOutputUnavailable() : output.Trim()]);
    }

    private async Task RunAsync(
        string? pendingResourceKey = null,
        string? errorResourceKey = null,
        string? successResourceKey = null,
        object?[]? actionArguments = null,
        Func<Task<string>>? action = null,
        Func<string, object?[]>? successArguments = null)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            return;
        }

        dispatch(() =>
        {
            Error = null;
            Status = new(pendingResourceKey ?? "ImagesLoading", actionArguments ?? []);
            Changed?.Invoke();
        });

        string? output = null;
        Exception? actionError = null;
        Exception? refreshError = null;
        IReadOnlyList<ImageInfo>? images = null;
        if (action is not null)
        {
            try
            {
                output = await action().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                actionError = exception;
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

            var refreshFailure = refreshError is null
                ? null
                : new LocalizedMessage("ImagesRefreshError", [refreshError.Message]);
            var actionFailure = actionError is null || errorResourceKey is null
                ? null
                : new LocalizedMessage(
                    errorResourceKey,
                    actionArguments?.Append(actionError.Message).ToArray() ?? [actionError.Message]);
            Error = actionFailure is not null && refreshFailure is not null
                ? new("ActionAndRefreshError", [actionFailure, refreshFailure])
                : actionFailure ?? refreshFailure;
            Status = action is not null && actionError is null && successResourceKey is not null
                ? new(successResourceKey, successArguments!(output ?? string.Empty))
                : null;
            Interlocked.Exchange(ref _busy, 0);
            Changed?.Invoke();
        });
    }

    private Task ReportInvalidImageReference()
    {
        dispatch(() =>
        {
            Error = new("ImagesInvalidReference", []);
            Status = null;
            Changed?.Invoke();
        });
        return Task.CompletedTask;
    }

    private static bool IsValidImageReference(string? image) =>
        !string.IsNullOrWhiteSpace(image) && image[0] != '-';

    private static LocalizedMessage AppOutputUnavailable() => new("ImagesPruneNoDetails", []);
}
