namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public interface IProxyConfigLintActiveConfigurationSource
{
    ProxyConfigLintActiveConfigurationReadResult Read();
}

public abstract record ProxyConfigLintActiveConfigurationReadResult
{
    private ProxyConfigLintActiveConfigurationReadResult()
    {
    }

    public static ProxyConfigLintActiveConfigurationReadResult MissingConfiguration { get; } =
        new MissingConfigurationResult();

    public static ProxyConfigLintActiveConfigurationReadResult Available(ProxyConfigLintConfigurationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new AvailableResult(snapshot);
    }

    public sealed record AvailableResult : ProxyConfigLintActiveConfigurationReadResult
    {
        public AvailableResult(ProxyConfigLintConfigurationSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            Snapshot = snapshot;
        }

        public ProxyConfigLintConfigurationSnapshot Snapshot { get; }
    }

    public sealed record MissingConfigurationResult : ProxyConfigLintActiveConfigurationReadResult;
}
