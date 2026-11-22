using System.Globalization;
using System.Text.Json;
using Wessl.Domain;

namespace Wessl.Infrastructure;

public static class WslcOutputParser
{
    public static ContainerUsage ParseUsage(string output)
    {
        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("WSLc output contains invalid container usage.");
            }

            var cpuPercent = GetString(root, "CPUPerc");
            var memoryUsage = GetString(root, "MemUsage");
            if (cpuPercent.Length == 0 || memoryUsage.Length == 0)
            {
                throw new FormatException("WSLc output is missing container usage information.");
            }

            return ReadUsage(root);
        }

        throw new FormatException("WSLc output contains no container usage.");
    }

    public static IReadOnlyDictionary<string, ContainerUsage> ParseUsages(string output)
    {
        var usages = new Dictionary<string, ContainerUsage>(StringComparer.Ordinal);
        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("WSLc output contains invalid container usage.");
            }
            var id = GetString(root, "ID");
            if (id.Length == 0 || !usages.TryAdd(id, ReadUsage(root)))
            {
                throw new FormatException("WSLc output contains a missing or duplicate container ID.");
            }
        }
        return usages;
    }

    public static ContainerDetails ParseContainerDetails(string output)
    {
        using var document = JsonDocument.Parse(output);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 1)
        {
            throw new FormatException("WSLc output must contain exactly one inspected container.");
        }

        var container = root[0];
        if (container.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("WSLc output contains an invalid container inspection.");
        }

        _ = RequireString(container, "Id");
        _ = RequireString(container, "Name");
        var config = GetOptionalObject(container, "Config");
        var state = GetOptionalObject(container, "State");

        return new ContainerDetails(
            config is { } configElement ? GetOptionalString(configElement, "Image") : null,
            GetOptionalString(container, "Image"),
            GetOptionalTimestamp(container, "Created"),
            state is { } stateElement ? GetOptionalTimestamp(stateElement, "StartedAt") : null,
            ReadCommand(config),
            ReadPorts(container),
            ReadMounts(container),
            ReadNetworks(container));
    }

    private static string RequireString(JsonElement element, string propertyName)
    {
        var value = GetOptionalString(element, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException($"WSLc output is missing container {propertyName}.");
        }

        return value;
    }

    private static JsonElement? GetOptionalObject(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException($"WSLc output contains an invalid {propertyName} object.");
        }

        return value;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new FormatException($"WSLc output contains an invalid {propertyName} value.");
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? GetOptionalTimestamp(JsonElement element, string propertyName)
    {
        var value = GetOptionalString(element, propertyName);
        return value is null || IsUnsetTimestamp(value) ? null : value;
    }

    private static bool IsUnsetTimestamp(string value) =>
        value.Equals("0001-01-01T00:00:00Z", StringComparison.OrdinalIgnoreCase)
        || value.Equals("0001-01-01T00:00:00.000000000Z", StringComparison.OrdinalIgnoreCase);

    private static string? ReadCommand(JsonElement? config)
    {
        if (config is not { } configElement)
        {
            return null;
        }

        var entrypoint = ReadOptionalStringArray(configElement, "Entrypoint");
        var command = ReadOptionalStringArray(configElement, "Cmd");
        if (entrypoint is null && command is null)
        {
            return null;
        }

        return JsonSerializer.Serialize((entrypoint ?? []).Concat(command ?? []).ToArray());
    }

    private static IReadOnlyList<string>? ReadOptionalStringArray(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException($"WSLc output contains an invalid {propertyName} array.");
        }

        var result = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new FormatException($"WSLc output contains an invalid value in {propertyName}.");
            }

            result.Add(item.GetString() ?? string.Empty);
        }

        return result;
    }

    private static IReadOnlyList<string>? ReadPorts(JsonElement container)
    {
        if (!TryGetProperty(container, "Ports", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("WSLc output contains an invalid ports object.");
        }

        var ports = new List<string>();
        foreach (var port in value.EnumerateObject())
        {
            if (port.Value.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException("WSLc output contains an invalid port bindings array.");
            }

            if (port.Value.GetArrayLength() == 0)
            {
                ports.Add(port.Name);
                continue;
            }

            foreach (var binding in port.Value.EnumerateArray())
            {
                if (binding.ValueKind != JsonValueKind.Object)
                {
                    throw new FormatException("WSLc output contains an invalid port binding.");
                }

                var hostIp = GetOptionalString(binding, "HostIp");
                var hostPort = GetOptionalString(binding, "HostPort");
                ports.Add(hostPort is null
                    ? port.Name
                    : $"{port.Name} → {(hostIp is null ? string.Empty : $"{hostIp}:")}{hostPort}");
            }
        }

        return ports;
    }

    private static IReadOnlyList<string>? ReadMounts(JsonElement container)
    {
        if (!TryGetProperty(container, "Mounts", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("WSLc output contains an invalid mounts array.");
        }

        var mounts = new List<string>();
        foreach (var mount in value.EnumerateArray())
        {
            if (mount.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("WSLc output contains an invalid mount.");
            }

            var type = GetOptionalString(mount, "Type") ?? string.Empty;
            var source = GetOptionalString(mount, "Source") ?? string.Empty;
            var destination = GetOptionalString(mount, "Destination") ?? string.Empty;
            var readWrite = GetOptionalBoolean(mount, "ReadWrite");
            mounts.Add($"{type}: {source} → {destination} ({(readWrite ? "read-write" : "read-only")})");
        }

        return mounts;
    }

    private static bool GetOptionalBoolean(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new FormatException($"WSLc output contains an invalid {propertyName} value.");
        }

        return value.GetBoolean();
    }

    private static IReadOnlyList<string>? ReadNetworks(JsonElement container)
    {
        var networkSettings = GetOptionalObject(container, "NetworkSettings");
        if (networkSettings is not { } settings)
        {
            return null;
        }

        if (!TryGetProperty(settings, "Networks", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("WSLc output contains an invalid networks object.");
        }

        var networks = new List<string>();
        foreach (var network in value.EnumerateObject())
        {
            if (network.Value.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("WSLc output contains an invalid network.");
            }

            networks.Add(network.Name);
        }

        return networks;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static ContainerUsage ReadUsage(JsonElement root)
    {
        var cpu = GetString(root, "CPUPerc");
        var memory = GetString(root, "MemUsage");
        if (cpu.Length == 0 || memory.Length == 0)
        {
            throw new FormatException("WSLc output is missing container usage information.");
        }
        return new(cpu, memory, GetString(root, "NetIO"), GetString(root, "BlockIO"),
            GetString(root, "PIDs"), GetString(root, "MemPerc"));
    }

    public static IReadOnlyList<ImageInfo> ParseImages(string output)
    {
        var images = new List<ImageInfo>();
        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("WSLc output contains an invalid image.");
            }

            var id = GetString(root, "ID");
            var repository = GetString(root, "Repository");
            var tag = GetString(root, "Tag");
            var size = GetString(root, "Size");
            var digest = GetString(root, "Digest");
            if (id.Length == 0 || repository.Length == 0 || tag.Length == 0 || size.Length == 0)
            {
                throw new FormatException("WSLc output is missing image information.");
            }

            images.Add(new ImageInfo(id, repository, tag, size, digest));
        }

        return images;
    }

    public static IReadOnlyList<ContainerInfo> Parse(string output)
    {
        var containers = new List<ContainerInfo>();
        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var id = GetString(root, "ID");
            var name = GetString(root, "Names");
            var image = GetString(root, "Image");
            var state = GetString(root, "State");
            if (state.Length == 0)
            {
                state = GetString(root, "Status");
            }

            if (id.Length == 0 || name.Length == 0)
            {
                throw new FormatException("WSLc output is missing a container ID or name.");
            }

            var (ports, publishedPorts) = GetPorts(root);
            containers.Add(new ContainerInfo(id, name, image, ParseState(state))
            {
                Ports = ports,
                PublishedPorts = publishedPorts,
                Labels = GetLabels(root)
            });
        }

        return containers;
    }

    private static IReadOnlyDictionary<string, string> GetLabels(JsonElement container)
    {
        foreach (var property in container.EnumerateObject())
        {
            if (!property.Name.Equals("Labels", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Null)
            {
                return new Dictionary<string, string>();
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new FormatException("WSLc output contains an invalid container labels value.");
            }

            var labels = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in (property.Value.GetString() ?? string.Empty)
                         .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = entry.IndexOf('=');
                if (separator <= 0
                    || !labels.TryAdd(entry[..separator], entry[(separator + 1)..]))
                {
                    throw new FormatException("WSLc output contains an invalid or duplicate container label.");
                }
            }

            return labels;
        }

        return new Dictionary<string, string>();
    }

    public static IReadOnlyDictionary<string, ContainerDiagnostics> ParseContainerDiagnostics(string output)
    {
        using var document = JsonDocument.Parse(output);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("WSLc inspect output is not an array.");
        }

        var diagnostics = new Dictionary<string, ContainerDiagnostics>(StringComparer.Ordinal);
        foreach (var container in root.EnumerateArray())
        {
            if (container.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("WSLc inspect output contains an invalid container.");
            }

            var id = GetRequiredDiagnosticString(container, "Id");
            if (!TryGetProperty(container, "State", out var state) || state.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("WSLc inspect output is missing container state.");
            }

            var health = ContainerHealthStatus.Unavailable;
            string? latestError = null;
            if (TryGetProperty(state, "Health", out var healthElement)
                && healthElement.ValueKind != JsonValueKind.Null)
            {
                if (healthElement.ValueKind != JsonValueKind.Object)
                {
                    throw new FormatException("WSLc inspect output contains an invalid health value.");
                }

                health = ParseHealthStatus(GetDiagnosticString(healthElement, "Status"));
                latestError = GetLatestHealthCheckError(healthElement);
            }

            var diagnosticsEntry = new ContainerDiagnostics(
                health,
                GetOptionalInt32(state, "ExitCode"),
                GetDiagnosticTimestamp(state, "FinishedAt"),
                latestError);
            if (!diagnostics.TryAdd(id, diagnosticsEntry))
            {
                throw new FormatException("WSLc inspect output contains a duplicate container ID.");
            }
        }

        return diagnostics;
    }

    private static string GetRequiredDiagnosticString(JsonElement element, string propertyName)
    {
        var value = GetDiagnosticString(element, propertyName);
        if (value.Length == 0)
        {
            throw new FormatException($"WSLc inspect output is missing {propertyName}.");
        }

        return value;
    }

    private static string GetDiagnosticString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return string.Empty;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new FormatException($"WSLc inspect output contains an invalid {propertyName} value.");
        }

        return value.GetString() ?? string.Empty;
    }

    private static int? GetOptionalInt32(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result))
        {
            throw new FormatException($"WSLc inspect output contains an invalid {propertyName} value.");
        }

        return result;
    }

    private static DateTimeOffset? GetDiagnosticTimestamp(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new FormatException($"WSLc inspect output contains an invalid {propertyName} value.");
        }

        var timestamp = value.GetString();
        if (string.IsNullOrWhiteSpace(timestamp))
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(
                timestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var result))
        {
            throw new FormatException($"WSLc inspect output contains an invalid {propertyName} value.");
        }

        return result == DateTimeOffset.MinValue ? null : result;
    }

    private static string? GetLatestHealthCheckError(JsonElement health)
    {
        if (!TryGetProperty(health, "Log", out var log))
        {
            return null;
        }

        if (log.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("WSLc inspect output contains an invalid health log.");
        }

        string? latestError = null;
        foreach (var entry in log.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("WSLc inspect output contains an invalid health log entry.");
            }

            var exitCode = GetOptionalInt32(entry, "ExitCode");
            var output = GetDiagnosticString(entry, "Output");
            if (exitCode is not null and not 0 && !string.IsNullOrWhiteSpace(output))
            {
                latestError = output;
            }
        }

        return latestError;
    }

    private static ContainerHealthStatus ParseHealthStatus(string status) =>
        status.ToLowerInvariant() switch
        {
            "starting" => ContainerHealthStatus.Starting,
            "healthy" => ContainerHealthStatus.Healthy,
            "unhealthy" => ContainerHealthStatus.Unhealthy,
            "" => ContainerHealthStatus.Unknown,
            _ => ContainerHealthStatus.Unknown
        };

    private static string GetString(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => property.Value.GetRawText(),
                    _ => string.Empty
                };
            }
        }

        return string.Empty;
    }

    private static (IReadOnlyList<string> Ports, IReadOnlyList<ExposedPort> PublishedPorts) GetPorts(JsonElement container)
    {
        var display = new List<string>();
        var published = new List<ExposedPort>();
        foreach (var property in container.EnumerateObject())
        {
            if (!property.Name.Equals("Ports", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.String)
            {
                AddMappings(property.Value.GetString(), display, published);
            }
            else if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var port in property.Value.EnumerateArray())
                {
                    if (port.ValueKind == JsonValueKind.String)
                    {
                        AddMappings(port.GetString(), display, published);
                    }
                    else if (port.ValueKind == JsonValueKind.Object)
                    {
                        AddPortObject(port, display, published);
                    }
                    else
                    {
                        throw new FormatException("WSLc output contains an invalid container port.");
                    }
                }
            }
            else if (property.Value.ValueKind != JsonValueKind.Null)
            {
                throw new FormatException("WSLc output contains an invalid container ports value.");
            }

            break;
        }

        return (display, published.Distinct().ToArray());
    }

    private static void AddMappings(string? value, ICollection<string> display, ICollection<ExposedPort> published)
    {
        foreach (var mapping in (value ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            display.Add(mapping);
            var arrow = mapping.IndexOf("->", StringComparison.Ordinal);
            string host;
            string target;
            if (arrow >= 0)
            {
                host = mapping[..arrow].Trim();
                var colon = host.LastIndexOf(':');
                host = colon >= 0 ? host[(colon + 1)..] : host;
                target = mapping[(arrow + 2)..].Trim();
            }
            else
            {
                var colon = mapping.IndexOf(':');
                if (colon < 0 || colon != mapping.LastIndexOf(':'))
                {
                    continue;
                }

                host = mapping[..colon];
                target = mapping[(colon + 1)..];
            }

            var slash = target.IndexOf('/');
            var containerPort = slash >= 0 ? target[..slash] : target;
            var protocol = slash >= 0 ? target[(slash + 1)..] : "tcp";
            if (TryParsePort(host, out var hostPort)
                && TryParsePort(containerPort, out var privatePort)
                && protocol.Length > 0)
            {
                published.Add(new ExposedPort(hostPort, privatePort, protocol));
            }
        }
    }

    private static void AddPortObject(JsonElement port, ICollection<string> display, ICollection<ExposedPort> published)
    {
        var hostPortText = GetString(port, "HostPort");
        if (hostPortText.Length == 0)
        {
            hostPortText = GetString(port, "PublicPort");
        }

        var containerPortText = GetString(port, "ContainerPort");
        if (containerPortText.Length == 0)
        {
            containerPortText = GetString(port, "PrivatePort");
        }

        var protocol = GetString(port, "Protocol");
        if (protocol.Length == 0)
        {
            protocol = GetString(port, "Type");
        }

        if (protocol.Length == 0)
        {
            protocol = "tcp";
        }

        if (TryParsePort(hostPortText, out var hostPort)
            && TryParsePort(containerPortText, out var containerPort))
        {
            published.Add(new ExposedPort(hostPort, containerPort, protocol));
            display.Add($"{hostPort}->{containerPort}/{protocol}");
        }
        else
        {
            throw new FormatException("WSLc output contains an invalid container port.");
        }
    }

    private static bool TryParsePort(string value, out int port) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out port)
        && port is >= 1 and <= 65535;

    private static ContainerState ParseState(string state)
    {
        if (state.StartsWith("running", StringComparison.OrdinalIgnoreCase)
            || state.StartsWith("up ", StringComparison.OrdinalIgnoreCase))
        {
            return ContainerState.Running;
        }

        if (state.StartsWith("exited", StringComparison.OrdinalIgnoreCase)
            || state.StartsWith("stopped", StringComparison.OrdinalIgnoreCase))
        {
            return ContainerState.Stopped;
        }

        return ContainerState.Unknown;
    }
}
