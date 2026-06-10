namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public sealed record ProxyConfigurationReadResult<TConfiguration>(
    bool Found,
    TConfiguration? Configuration)
    where TConfiguration : class;
