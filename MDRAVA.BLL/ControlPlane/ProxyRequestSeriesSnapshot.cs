namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyRequestSeriesSnapshot(
    string Site,
    string Route,
    string Action,
    string StatusClass,
    long Count);
