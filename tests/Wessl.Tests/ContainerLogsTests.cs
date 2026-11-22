using System.Diagnostics;
using System.Runtime.CompilerServices;
using Wessl.App.Services;
using Wessl.Domain;
using Wessl.Infrastructure;

namespace Wessl.Tests;

public sealed class ContainerLogsTests
{
    [Fact]
    public void FollowCommandUsesArgumentList()
    {
        var info = new WslcClient().CreateLogsStartInfo("container-123");

        Assert.Equal("wslc", info.FileName);
        Assert.Equal(["container", "logs", "--timestamps", "-f", "container-123"], info.ArgumentList);
        Assert.True(info.RedirectStandardOutput);
        Assert.True(info.RedirectStandardError);
    }

    [Fact]
    public void FilterTextMatchesLinesWithoutChangingTheStoredText()
    {
        var text = string.Join(Environment.NewLine, "INFO ready", "ERROR failed", "warning later");

        Assert.Equal("ERROR failed", ContainerLogsSession.FilterText(text, "eRr"));
        Assert.Equal(text, ContainerLogsSession.FilterText(text, string.Empty));
        Assert.Equal(string.Empty, ContainerLogsSession.FilterText(text, "missing"));
    }

    [Theory]
    [InlineData("clipboard")]
    [InlineData("file")]
    public async Task CopyAndSaveFailuresRemainErrors(string operationName)
    {
        var result = await LogOperation.RunAsync(
            () => Task.FromException(new IOException($"{operationName} denied")),
            "Operation completed",
            exception => $"{operationName} failed: {exception.Message}");

        Assert.False(result.IsSuccess);
        Assert.Equal($"{operationName} failed: {operationName} denied", result.Message);
    }

    [Fact]
    public async Task SaveToFileWritesTheAvailableLogContent()
    {
        var session = new ContainerLogsSession(new SingleLineLogsClient("first line"), action => action());
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Changed += () => changed.TrySetResult();
        session.Start(new("id", "name", "image", ContainerState.Running));
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var path = Path.Combine(Path.GetTempPath(), $"wessl-logs-{Guid.NewGuid():N}.txt");
        try
        {
            await session.SaveToFileAsync(path);

            Assert.Equal($"first line{Environment.NewLine}", await File.ReadAllTextAsync(path));
        }
        finally
        {
            session.Stop();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task FollowStreamsBothOutputsAndStopsProcessOnCancellation()
    {
        if (!OperatingSystem.IsWindows()) return;

        var client = ClientWithScript(
            "[Console]::Out.WriteLine($PID); [Console]::Error.WriteLine('error stream'); Start-Sleep -Seconds 30");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var stream = client.FollowLogsAsync("id", cancellation.Token).GetAsyncEnumerator();
        var firstLines = new List<string>();
        while (firstLines.Count < 2)
        {
            Assert.True(await stream.MoveNextAsync());
            firstLines.Add(stream.Current);
        }

        Assert.Contains("error stream", firstLines);
        var pid = int.Parse(firstLines.Single(line => line != "error stream"));
        using var process = Process.GetProcessById(pid);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await stream.MoveNextAsync());
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(process.HasExited);
    }

    [Fact]
    public async Task FollowReportsProcessFailuresAndMissingExecutable()
    {
        if (!OperatingSystem.IsWindows()) return;

        var client = ClientWithScript("[Console]::Error.WriteLine('bad container'); exit 7");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in client.FollowLogsAsync("id")) { }
        });
        Assert.Contains("7", error.Message);
        Assert.Contains("bad container", error.Message);

        var missing = new WslcClient(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "wslc"));
        await Assert.ThrowsAsync<WslcUnavailableException>(async () =>
        {
            await foreach (var _ in missing.FollowLogsAsync("id")) { }
        });
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in client.FollowLogsAsync("--help")) { }
        });
    }

    [Fact]
    public async Task SampleRejectsMissingAndStoppedContainers()
    {
        var client = new SampleWslcClient();
        foreach (var id in new[] { "missing", "sample-2" })
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await foreach (var _ in client.FollowLogsAsync(id)) { }
            });
        }
    }

    [Fact]
    public async Task SessionShowsErrorsAndDoesNotStartForStoppedContainers()
    {
        var client = new FailingLogsClient();
        var session = new ContainerLogsSession(client, action => action());
        session.Start(new("id", "name", "image", ContainerState.Stopped));
        Assert.Equal("LogsNotRunning", session.Error!.ResourceKey);
        Assert.Equal(0, client.Calls);

        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = new ContainerLogsSession(client, action => action());
        running.Changed += () => changed.TrySetResult();
        running.Start(new("id", "name", "image", ContainerState.Running));
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("LogsError", running.Error!.ResourceKey);
        Assert.Equal("failed", running.Error.Arguments[0]);
        Assert.False(running.IsFollowing);
        running.Stop();
    }

    private static WslcClient ClientWithScript(string script) => new("wslc", _ =>
    {
        var info = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { "-NoProfile", "-Command", script })
        {
            info.ArgumentList.Add(argument);
        }
        return info;
    });

    private sealed class FailingLogsClient : IContainerLogsClient
    {
        public int Calls { get; private set; }

        public async IAsyncEnumerable<string> FollowLogsAsync(
            string id, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Calls++;
            await Task.Yield();
            throw new InvalidOperationException("failed");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
    }

    private sealed class SingleLineLogsClient(string line) : IContainerLogsClient
    {
        public async IAsyncEnumerable<string> FollowLogsAsync(
            string id, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return line;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
