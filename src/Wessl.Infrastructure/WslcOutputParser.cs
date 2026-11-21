using System.Text.Json;
using Wessl.Domain;

namespace Wessl.Infrastructure;

public static class WslcOutputParser
{
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

            containers.Add(new ContainerInfo(id, name, image, ParseState(state))
            {
                Ports = ParsePorts(root)
            });
        }

        return containers;
    }

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

    private static IReadOnlyList<ExposedPort> ParsePorts(JsonElement container)
    {
        if (!TryGetProperty(container, "Ports", out var ports))
        {
            return Array.Empty<ExposedPort>();
        }

        var parsedPorts = new List<ExposedPort>();
        if (ports.ValueKind == JsonValueKind.String)
        {
            ParsePortMappings(ports.GetString() ?? string.Empty, parsedPorts);
        }
        else if (ports.ValueKind == JsonValueKind.Array)
        {
            foreach (var port in ports.EnumerateArray())
            {
                if (port.ValueKind == JsonValueKind.String)
                {
                    ParsePortMappings(port.GetString() ?? string.Empty, parsedPorts);
                }
                else if (port.ValueKind == JsonValueKind.Object)
                {
                    ParsePortObject(port, parsedPorts);
                }
            }
        }

        return parsedPorts.Count == 0
            ? Array.Empty<ExposedPort>()
            : parsedPorts.Distinct().ToArray();
    }

    private static void ParsePortMappings(string mappings, ICollection<ExposedPort> ports)
    {
        foreach (var mapping in mappings.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var arrow = mapping.IndexOf("->", StringComparison.Ordinal);
            if (arrow < 0)
            {
                continue;
            }

            var host = mapping[..arrow].Trim();
            var colon = host.LastIndexOf(':');
            var hostPortText = colon >= 0 ? host[(colon + 1)..] : host;
            var target = mapping[(arrow + 2)..].Trim();
            var slash = target.IndexOf('/');
            var containerPortText = slash >= 0 ? target[..slash] : target;
            var protocol = slash >= 0 ? target[(slash + 1)..] : "tcp";
            if (TryParsePort(hostPortText, out var hostPort)
                && TryParsePort(containerPortText, out var containerPort)
                && protocol.Length > 0)
            {
                ports.Add(new ExposedPort(hostPort, containerPort, protocol));
            }
        }
    }

    private static void ParsePortObject(JsonElement port, ICollection<ExposedPort> ports)
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
            ports.Add(new ExposedPort(hostPort, containerPort, protocol));
        }
    }

    private static bool TryParsePort(string value, out int port) =>
        int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out port)
        && port is >= 1 and <= 65535;

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
