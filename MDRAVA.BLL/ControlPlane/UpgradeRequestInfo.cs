namespace MDRAVA.BLL.ControlPlane;

public sealed record UpgradeRequestInfo(
    string Protocol,
    bool IsWebSocket,
    string? WebSocketKey);
