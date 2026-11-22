using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Wessl.Domain;

namespace Wessl.Infrastructure;

public sealed class WslcClient : IWslcClient, IContainerLogsClient, IImageClient
{
    private readonly string _executable;
    private readonly Func<string, ProcessStartInfo>? _logsStartInfo;

    public WslcClient() : this("wslc") { }

    internal WslcClient(string executable) => _executable = executable;

    internal WslcClient(string executable, Func<string, ProcessStartInfo> logsStartInfo)
        : this(executable) => _logsStartInfo = logsStartInfo;

    public async Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default)
    {
        var output = await RunCommandAsync(cancellationToken, "container", "list", "--all", "--format", "json");
        return WslcOutputParser.Parse(output);
    }

    public async Task<IReadOnlyDictionary<string, ContainerDiagnostics>> GetContainerDiagnosticsAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            return new Dictionary<string, ContainerDiagnostics>(StringComparer.Ordinal);
        }

        var output = await RunCommandAsync(CreateContainerInspectStartInfo(ids), cancellationToken);
        return WslcOutputParser.ParseContainerDiagnostics(output);
    }

    public async Task<ContainerUsage> GetContainerUsageAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var output = await RunCommandAsync(CreateStatsStartInfo(id), cancellationToken);
        return WslcOutputParser.ParseUsage(output);
    }

    public async Task<IReadOnlyDictionary<string, ContainerUsage>> GetContainerUsagesAsync(
        IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        var output = await RunCommandAsync(cancellationToken, "stats", "--format", "json");
        var usages = WslcOutputParser.ParseUsages(output);
        var result = new Dictionary<string, ContainerUsage>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            var matches = usages.Where(pair => pair.Key.StartsWith(id, StringComparison.Ordinal)).ToArray();
            if (matches.Length > 1)
            {
                throw new FormatException("WSLc output contains ambiguous container IDs.");
            }
            if (matches.Length == 1)
            {
                result.Add(id, matches[0].Value);
            }
        }
        return result;
    }

    public async Task<IReadOnlyList<ImageInfo>> ListImagesAsync(CancellationToken cancellationToken = default)
    {
        var output = await RunCommandAsync(cancellationToken, "image", "list", "--format", "json");
        return WslcOutputParser.ParseImages(output);
    }

    public Task<string> PruneImagesAsync(CancellationToken cancellationToken = default) =>
        RunCommandAsync(cancellationToken, "image", "prune", "--all", "--force");

    public Task StartContainerAsync(string id, CancellationToken cancellationToken = default) =>
        RunCommandAsync(cancellationToken, "container", "start", id);

    public Task StopContainerAsync(string id, CancellationToken cancellationToken = default) =>
        RunCommandAsync(cancellationToken, "container", "stop", id);

    public async Task RestartContainerAsync(string id, CancellationToken cancellationToken = default)
    {
        await StopContainerAsync(id, cancellationToken);
        await StartContainerAsync(id, cancellationToken);
    }

    public Task RemoveContainerAsync(string id, CancellationToken cancellationToken = default) =>
        RunCommandAsync(cancellationToken, "remove", "--force", id);

    public async IAsyncEnumerable<string> FollowLogsAsync(
        string id,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        cancellationToken.ThrowIfCancellationRequested();
        if (id.StartsWith('-'))
        {
            throw new ArgumentException("The container ID cannot start with a hyphen.", nameof(id));
        }

        var startInfo = _logsStartInfo?.Invoke(id) ?? CreateLogsStartInfo(id);
        Process? startedProcess;
        try
        {
            startedProcess = Process.Start(startInfo);
        }
        catch (Exception exception) when (exception is Win32Exception or UnauthorizedAccessException)
        {
            throw new WslcUnavailableException(exception.Message, exception);
        }

        using var process = startedProcess ?? throw new WslcUnavailableException("Could not start wslc.");
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var lines = Channel.CreateBounded<string>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });
        var error = new StringBuilder();

        async Task ReadLinesAsync(StreamReader reader, bool isError)
        {
            while (await reader.ReadLineAsync(stop.Token).ConfigureAwait(false) is { } line)
            {
                if (isError && error.Length < 4096)
                {
                    error.AppendLine(line[..Math.Min(line.Length, 4096 - error.Length)]);
                }

                await lines.Writer.WriteAsync(line, stop.Token).ConfigureAwait(false);
            }
        }

        async Task ReadProcessAsync()
        {
            try
            {
                await Task.WhenAll(
                    ReadLinesAsync(process.StandardOutput, false),
                    ReadLinesAsync(process.StandardError, true)).ConfigureAwait(false);
                await process.WaitForExitAsync(stop.Token).ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"wslc exited with code {process.ExitCode}: {error}");
                }

                lines.Writer.TryComplete();
            }
            catch (Exception exception)
            {
                lines.Writer.TryComplete(exception);
            }
        }

        var readerTask = ReadProcessAsync();
        try
        {
            await foreach (var line in lines.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return line;
            }
            await readerTask.ConfigureAwait(false);
        }
        finally
        {
            stop.Cancel();
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                }
            }
            await readerTask.ConfigureAwait(false);
        }
    }

    internal ProcessStartInfo CreateLogsStartInfo(string id)
    {
        var startInfo = new ProcessStartInfo(_executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { "container", "logs", "--timestamps", "-f", id })
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    internal ProcessStartInfo CreateStatsStartInfo(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (id.StartsWith('-'))
        {
            throw new ArgumentException("The container ID cannot start with a hyphen.", nameof(id));
        }

        var startInfo = CreateStartInfo();
        foreach (var argument in new[] { "stats", "--format", "json", id })
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    internal ProcessStartInfo CreateContainerInspectStartInfo(IReadOnlyCollection<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var startInfo = CreateStartInfo();
        foreach (var argument in new[] { "container", "inspect", "--format", "json" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var id in ids)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            if (id.StartsWith('-'))
            {
                throw new ArgumentException("A container ID cannot start with a hyphen.", nameof(ids));
            }

            startInfo.ArgumentList.Add(id);
        }

        return startInfo;
    }

    private async Task<string> RunCommandAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        var startInfo = CreateStartInfo();
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return await RunCommandAsync(startInfo, cancellationToken);
    }

    private ProcessStartInfo CreateStartInfo() => new(_executable)
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };

    private async Task<string> RunCommandAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Process? startedProcess;
        try
        {
            startedProcess = Process.Start(startInfo);
        }
        catch (Exception exception) when (exception is Win32Exception or UnauthorizedAccessException)
        {
            throw new WslcUnavailableException(exception.Message, exception);
        }

        using var process = startedProcess ?? throw new WslcUnavailableException("Could not start wslc.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            var stdout = await output;
            var stderr = await error;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"wslc exited with code {process.ExitCode}: {stderr}");
            }

            return stdout;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                }
            }

            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
    }
}
