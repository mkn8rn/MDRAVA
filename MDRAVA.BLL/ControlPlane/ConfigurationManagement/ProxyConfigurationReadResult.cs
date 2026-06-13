namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public abstract record ProxyConfigurationReadResult<TConfiguration>
    where TConfiguration : class
{
    private ProxyConfigurationReadResult()
    {
    }

    public static ProxyConfigurationReadResult<TConfiguration> MissingConfiguration { get; } =
        new MissingConfigurationResult();

    public static ProxyConfigurationReadResult<TConfiguration> Available(TConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new AvailableResult(configuration);
    }

    public sealed record AvailableResult : ProxyConfigurationReadResult<TConfiguration>
    {
        public AvailableResult(TConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            Configuration = configuration;
        }

        public TConfiguration Configuration { get; }
    }

    public sealed record MissingConfigurationResult : ProxyConfigurationReadResult<TConfiguration>;
}
