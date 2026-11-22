using Wessl.App.Services;
using Wessl.Domain;
using Wessl.Infrastructure;

namespace Wessl.Tests;

public sealed class ImageManagementTests
{
    [Fact]
    public void ParsesWslcJsonLinesIncludingUntaggedImagesAndSizes()
    {
        var images = WslcOutputParser.ParseImages("""
            {"ID":"abc","Repository":"nginx","Tag":"latest","Size":"123MB","Digest":"sha256:abc"}

            {"id":"def","repository":"<none>","tag":"<none>","size":"42.1MB"}
            """);

        Assert.Equal(2, images.Count);
        Assert.Equal(new ImageInfo("abc", "nginx", "latest", "123MB", "sha256:abc"), images[0]);
        Assert.Equal(new ImageInfo("def", "<none>", "<none>", "42.1MB"), images[1]);
        Assert.Empty(WslcOutputParser.ParseImages(""));
    }

    [Theory]
    [InlineData("{\"ID\":\"abc\",\"Repository\":\"nginx\",\"Tag\":\"latest\"}")]
    [InlineData("[]")]
    public void RejectsIncompleteOrInvalidImageRecords(string output) =>
        Assert.Throws<FormatException>(() => WslcOutputParser.ParseImages(output));

    [Fact]
    public async Task LoadsOnlyOnRequestAndPruneRefreshesAndReportsOutput()
    {
        var client = new ImageClient();
        var service = new ImageService(client, action => action());
        Assert.Equal(0, client.ListCalls);
        Assert.Equal(0, client.PruneCalls);

        await service.RefreshAsync();
        Assert.Single(service.Images);
        await service.PruneAsync();

        Assert.Equal(2, client.ListCalls);
        Assert.Equal(1, client.PruneCalls);
        Assert.True(service.HasLoaded);
        Assert.Equal("ImagesPruneSuccess", service.Status!.ResourceKey);
        Assert.Equal("Total reclaimed space: 42MB", service.Status.Arguments[0]);
        Assert.Null(service.Error);
    }

    [Fact]
    public async Task ReportsBothPruneAndRefreshErrors()
    {
        var client = new ImageClient { PruneFailure = true, ListFailure = true };
        var service = new ImageService(client, action => action());
        await service.PruneAsync();
        Assert.Equal("ActionAndRefreshError", service.Error!.ResourceKey);
        Assert.Equal("ImagesPruneError", Assert.IsType<LocalizedMessage>(service.Error.Arguments[0]).ResourceKey);
        Assert.Equal("ImagesRefreshError", Assert.IsType<LocalizedMessage>(service.Error.Arguments[1]).ResourceKey);
        Assert.Null(service.Status);
        Assert.False(service.HasLoaded);
        Assert.False(service.IsBusy);
    }

    [Fact]
    public async Task PruneSuccessIsPreservedWhenRefreshFails()
    {
        var client = new ImageClient { ListFailure = true };
        var service = new ImageService(client, action => action());
        await service.PruneAsync();
        Assert.Equal("ImagesPruneSuccess", service.Status!.ResourceKey);
        Assert.Equal("ImagesRefreshError", service.Error!.ResourceKey);
    }

    [Fact]
    public async Task PullAndTagRunExplicitlyAndReportTheirResults()
    {
        var client = new ImageClient();
        var service = new ImageService(client, action => action());

        await service.PullImageAsync("nginx:latest");
        Assert.Equal("nginx:latest", client.PulledImage);
        Assert.Equal("ImagesPullSuccess", service.Status!.ResourceKey);
        Assert.Null(service.Error);

        await service.TagImageAsync("nginx:latest", "local/nginx:dev");
        Assert.Equal(("nginx:latest", "local/nginx:dev"), (client.TagSource, client.TagTarget));
        Assert.Equal("ImagesTagSuccess", service.Status!.ResourceKey);
        Assert.Null(service.Error);
    }

    [Fact]
    public async Task PullFailureIsReportedWithoutSuccessStatus()
    {
        var client = new ImageClient { PullFailure = true };
        var service = new ImageService(client, action => action());

        await service.PullImageAsync("nginx:latest");

        Assert.Equal("ImagesPullError", service.Error!.ResourceKey);
        Assert.Equal("pull failed", service.Error.Arguments[1]);
        Assert.Null(service.Status);
        Assert.False(service.IsBusy);
    }

    [Fact]
    public async Task ReportsInvalidImageReferencesBeforeCallingClient()
    {
        var client = new ImageClient();
        var service = new ImageService(client, action => action());

        await service.PullImageAsync("--help");
        Assert.Equal("ImagesInvalidReference", service.Error!.ResourceKey);

        await service.TagImageAsync("nginx:latest", " ");
        Assert.Equal("ImagesInvalidReference", service.Error!.ResourceKey);

        Assert.Null(client.PulledImage);
        Assert.Null(client.TagSource);
    }

    [Fact]
    public async Task IgnoresConcurrentActionsUntilRefreshCompletes()
    {
        var client = new BlockingImageClient();
        var service = new ImageService(client, action => action());
        var refresh = service.RefreshAsync();
        Assert.True(service.IsBusy);
        await service.PruneAsync();
        Assert.Equal(0, client.PruneCalls);
        client.Complete();
        await refresh;
        Assert.False(service.IsBusy);
        Assert.True(service.HasLoaded);
    }

    private sealed class BlockingImageClient : IImageClient
    {
        private readonly TaskCompletionSource<IReadOnlyList<ImageInfo>> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int PruneCalls { get; private set; }
        public Task<IReadOnlyList<ImageInfo>> ListImagesAsync(CancellationToken cancellationToken = default) => _result.Task;
        public Task<string> PruneImagesAsync(CancellationToken cancellationToken = default)
        {
            PruneCalls++;
            return Task.FromResult("");
        }

        public Task<string> PullImageAsync(string image, CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Empty);

        public Task<string> TagImageAsync(string source, string target, CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Empty);

        public void Complete() => _result.SetResult([]);
    }

    private sealed class ImageClient : IImageClient
    {
        public int ListCalls { get; private set; }
        public int PruneCalls { get; private set; }
        public string? PulledImage { get; private set; }
        public string? TagSource { get; private set; }
        public string? TagTarget { get; private set; }
        public bool PruneFailure { get; init; }
        public bool PullFailure { get; init; }
        public bool ListFailure { get; init; }

        public Task<IReadOnlyList<ImageInfo>> ListImagesAsync(CancellationToken cancellationToken = default)
        {
            ListCalls++;
            return ListFailure
                ? Task.FromException<IReadOnlyList<ImageInfo>>(new InvalidOperationException("list failed"))
                : Task.FromResult<IReadOnlyList<ImageInfo>>([new("id", "repo", "tag", "1 MB")]);
        }

        public Task<string> PruneImagesAsync(CancellationToken cancellationToken = default)
        {
            PruneCalls++;
            return PruneFailure
                ? Task.FromException<string>(new InvalidOperationException("prune failed"))
                : Task.FromResult("Total reclaimed space: 42MB\n");
        }

        public Task<string> PullImageAsync(string image, CancellationToken cancellationToken = default)
        {
            PulledImage = image;
            return PullFailure
                ? Task.FromException<string>(new InvalidOperationException("pull failed"))
                : Task.FromResult("download complete");
        }

        public Task<string> TagImageAsync(string source, string target, CancellationToken cancellationToken = default)
        {
            TagSource = source;
            TagTarget = target;
            return Task.FromResult("tag complete");
        }
    }
}
