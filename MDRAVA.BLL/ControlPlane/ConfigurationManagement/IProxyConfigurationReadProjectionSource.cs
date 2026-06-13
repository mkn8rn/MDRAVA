namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public interface IProxyConfigurationReadProjectionSource<TConfiguration>
    where TConfiguration : class
{
    ProxyConfigurationReadProjectionResult<TConfiguration> ReadCurrent();
}

public abstract record ProxyConfigurationReadProjectionResult<TConfiguration>
    where TConfiguration : class
{
    private ProxyConfigurationReadProjectionResult()
    {
    }

    public static ProxyConfigurationReadProjectionResult<TConfiguration> MissingConfiguration { get; } =
        new MissingConfigurationResult();

    public static ProxyConfigurationReadProjectionResult<TConfiguration> Available(TConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new AvailableResult(configuration);
    }

    public sealed record AvailableResult : ProxyConfigurationReadProjectionResult<TConfiguration>
    {
        public AvailableResult(TConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            Configuration = configuration;
        }

        public TConfiguration Configuration { get; }
    }

    public sealed record MissingConfigurationResult : ProxyConfigurationReadProjectionResult<TConfiguration>;
}
