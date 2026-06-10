namespace MDRAVA.BLL.ControlPlane.Upgrades;

public sealed record UpgradeRequestInfo(
    string Protocol,
    bool IsWebSocket,
    string? WebSocketKey);
