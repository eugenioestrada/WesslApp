using System.Globalization;
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

            var (ports, publishedPorts) = GetPorts(root);
            containers.Add(new ContainerInfo(id, name, image, ParseState(state))
            {
                Ports = ports,
                PublishedPorts = publishedPorts
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
