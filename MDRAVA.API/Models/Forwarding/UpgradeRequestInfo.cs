namespace MDRAVA.API.Models.Forwarding;

public sealed record UpgradeRequestInfo(
    string Protocol,
    bool IsWebSocket,
    string? WebSocketKey);
