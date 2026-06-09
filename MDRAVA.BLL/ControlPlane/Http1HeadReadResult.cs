namespace MDRAVA.BLL.ControlPlane;

public sealed record Http1HeadReadResult(
    int HeadLength,
    int TotalBytesRead,
    ReadOnlyMemory<byte> HeadBytes,
    ReadOnlyMemory<byte> InitialBodyBytes);
