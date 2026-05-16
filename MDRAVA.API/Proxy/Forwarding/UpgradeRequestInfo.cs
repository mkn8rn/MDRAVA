namespace MDRAVA.API.Proxy.Forwarding;

public sealed record UpgradeRequestInfo(
    string Protocol,
    bool IsWebSocket,
    string? WebSocketKey);
