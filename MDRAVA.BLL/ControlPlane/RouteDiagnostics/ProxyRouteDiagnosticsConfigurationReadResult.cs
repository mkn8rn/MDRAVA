namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public abstract record ProxyRouteDiagnosticsConfigurationReadResult
{
    private ProxyRouteDiagnosticsConfigurationReadResult()
    {
    }

    public static ProxyRouteDiagnosticsConfigurationReadResult MissingConfiguration { get; } =
        new MissingConfigurationResult();

    public static ProxyRouteDiagnosticsConfigurationReadResult Available(
        IProxyRouteDiagnosticsConfigurationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new AvailableResult(snapshot);
    }

    public sealed record AvailableResult : ProxyRouteDiagnosticsConfigurationReadResult
    {
        public AvailableResult(IProxyRouteDiagnosticsConfigurationSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            Snapshot = snapshot;
        }

        public IProxyRouteDiagnosticsConfigurationSnapshot Snapshot { get; }
    }

    public sealed record MissingConfigurationResult : ProxyRouteDiagnosticsConfigurationReadResult;
}
