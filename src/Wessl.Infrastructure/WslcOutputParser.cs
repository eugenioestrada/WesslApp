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
                Ports = GetPorts(root)
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
                return property.Value.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> GetPorts(JsonElement element)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!property.Name.Equals("Ports", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.String)
            {
                return SplitPorts(property.Value.GetString());
            }

            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                var ports = new List<string>();
                foreach (var port in property.Value.EnumerateArray())
                {
                    if (port.ValueKind != JsonValueKind.String)
                    {
                        throw new FormatException("WSLc output contains an invalid container port.");
                    }

                    ports.AddRange(SplitPorts(port.GetString()));
                }

                return ports;
            }

            if (property.Value.ValueKind != JsonValueKind.Null)
            {
                throw new FormatException("WSLc output contains an invalid container ports value.");
            }

            return [];
        }

        return [];
    }

    private static IReadOnlyList<string> SplitPorts(string? ports) =>
        string.IsNullOrWhiteSpace(ports)
            ? []
            : ports.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

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
