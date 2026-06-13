using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public interface IProxyConfigurationStore
{
    bool HasActiveSnapshot { get; }

    ProxyConfigurationSnapshot Snapshot { get; }

    ProxyConfigurationSnapshotReadResult ReadSnapshot();

    ProxyConfigurationSnapshot Replace(ProxyConfigurationSnapshot snapshot);
}

public abstract record ProxyConfigurationSnapshotReadResult
{
    private ProxyConfigurationSnapshotReadResult()
    {
    }

    public static ProxyConfigurationSnapshotReadResult MissingSnapshot { get; } = new MissingSnapshotResult();

    public static ProxyConfigurationSnapshotReadResult Available(ProxyConfigurationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new AvailableResult(snapshot);
    }

    public sealed record AvailableResult : ProxyConfigurationSnapshotReadResult
    {
        public AvailableResult(ProxyConfigurationSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            Snapshot = snapshot;
        }

        public ProxyConfigurationSnapshot Snapshot { get; }
    }

    public sealed record MissingSnapshotResult : ProxyConfigurationSnapshotReadResult;
}
