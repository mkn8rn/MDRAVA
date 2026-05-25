namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyConfigurationReadResult<TConfiguration>(
    bool Found,
    TConfiguration? Configuration)
    where TConfiguration : class;
