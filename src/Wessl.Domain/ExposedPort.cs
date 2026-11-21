namespace Wessl.Domain;

public sealed record ExposedPort(int HostPort, int ContainerPort, string Protocol);
