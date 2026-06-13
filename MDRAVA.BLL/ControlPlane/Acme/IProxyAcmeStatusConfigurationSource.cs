namespace MDRAVA.BLL.ControlPlane.Acme;

public interface IProxyAcmeStatusConfigurationSource
{
    ProxyAcmeStatusConfigurationSourceReadResult Read();
}

public abstract record ProxyAcmeStatusConfigurationSourceReadResult
{
    private ProxyAcmeStatusConfigurationSourceReadResult()
    {
    }

    public static ProxyAcmeStatusConfigurationSourceReadResult MissingConfiguration { get; } =
        new MissingConfigurationResult();

    public static ProxyAcmeStatusConfigurationSourceReadResult Available(
        ProxyAcmeStatusConfigurationSourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new AvailableResult(snapshot);
    }

    public sealed record AvailableResult : ProxyAcmeStatusConfigurationSourceReadResult
    {
        public AvailableResult(ProxyAcmeStatusConfigurationSourceSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            Snapshot = snapshot;
        }

        public ProxyAcmeStatusConfigurationSourceSnapshot Snapshot { get; }
    }

    public sealed record MissingConfigurationResult : ProxyAcmeStatusConfigurationSourceReadResult;
}
