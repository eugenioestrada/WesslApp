namespace Wessl.Domain;

public sealed record ContainerUsage(
    string CpuPercent,
    string MemoryUsage,
    string NetworkIo = "",
    string BlockIo = "",
    string Processes = "",
    string MemoryPercent = "");
