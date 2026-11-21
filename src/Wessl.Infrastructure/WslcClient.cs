using System.Diagnostics;
using Wessl.Domain;

namespace Wessl.Infrastructure;

public sealed class WslcClient : IWslcClient
{
    public async Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default)
    {
        var output = await RunCommandAsync(cancellationToken, "container", "list", "--all", "--format", "json");
        return WslcOutputParser.Parse(output);
    }

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

    private static async Task<string> RunCommandAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("wslc")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start wslc.");
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
                process.Kill(entireProcessTree: true);
            }

            throw;
        }
    }
}
