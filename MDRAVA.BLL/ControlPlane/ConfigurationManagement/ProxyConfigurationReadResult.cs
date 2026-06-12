namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public sealed record ProxyConfigurationReadResult<TConfiguration>
    where TConfiguration : class
{
    private ProxyConfigurationReadResult(bool found, TConfiguration? configuration)
    {
        Found = found;
        Configuration = configuration;
    }

    public bool Found { get; }

    public TConfiguration? Configuration { get; }

    public static ProxyConfigurationReadResult<TConfiguration> Available(TConfiguration configuration)
    {
        return new ProxyConfigurationReadResult<TConfiguration>(
            found: true,
            configuration: configuration);
    }

    public static ProxyConfigurationReadResult<TConfiguration> Missing()
    {
        return new ProxyConfigurationReadResult<TConfiguration>(
            found: false,
            configuration: null);
    }
}
