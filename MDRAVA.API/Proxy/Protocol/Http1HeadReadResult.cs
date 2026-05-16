namespace MDRAVA.API.Proxy.Protocol;

public sealed record Http1HeadReadResult(
    int HeadLength,
    int TotalBytesRead,
    ReadOnlyMemory<byte> HeadBytes,
    ReadOnlyMemory<byte> InitialBodyBytes);
